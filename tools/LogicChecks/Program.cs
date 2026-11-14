using System.Reflection;
using UnityEngine;

internal static class Program
{
    private static int assertions;
    private static int passed;
    private static int failed;

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Invoke(object target, string method)
    {
        var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().Name, method);
        try { info.Invoke(target, null); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static async Task Run(string name, Func<Task> action)
    {
        try
        {
            await action();
            passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception error)
        {
            failed++;
            Console.WriteLine($"FAIL {name}: {error.Message}");
        }
    }

    private static Task Sync(Action action) { action(); return Task.CompletedTask; }

    private sealed class TrafficFixture : IDisposable
    {
        public readonly TrafficPoolManager Pool = new();
        public readonly TrafficManager Manager = new();
        public readonly List<TrafficStatRecord> Outcomes = new();
        public int Delivered;
        public int Dropped;
        public int Completed;

        public TrafficFixture()
        {
            Simulation.Instance = new Simulation();
            Invoke(Pool, "Awake");
            Invoke(Manager, "Awake");
            Invoke(Manager, "OnEnable");
            NetworkEventBus.OnTrafficStatRecorded += Record;
            NetworkEventBus.OnTrafficDelivered += OnDelivered;
            NetworkEventBus.OnTrafficDropped += OnDropped;
        }

        private void Record(TrafficStatRecord value) => Outcomes.Add(value);
        private void OnDelivered(float value) => Delivered++;
        private void OnDropped(float value) => Dropped++;
        public List<Packet> Start(float ttl = 1f, int count = 3, float size = 10.5f)
            => Manager.RequestTraffic("user", "server", size, count, ttl, () => Completed++);
        public void Tick(float now)
        {
            Simulation.Instance.Now = now;
            SimulationEventBus.RaisePostTick(0.1f);
        }
        public void Dispose()
        {
            NetworkEventBus.OnTrafficStatRecorded -= Record;
            NetworkEventBus.OnTrafficDelivered -= OnDelivered;
            NetworkEventBus.OnTrafficDropped -= OnDropped;
            Invoke(Manager, "OnDisable");
            Invoke(Manager, "OnDestroy");
            Invoke(Pool, "OnDestroy");
            Simulation.Instance = null;
        }
    }

    private static (SaveManager Manager, MemoryStorage Local, ControlledCloud Cloud) SaveFixture()
    {
        var local = new MemoryStorage();
        var manager = new SaveManager();
        manager.SetStorage(local);
        var cloud = new ControlledCloud();
        manager.EnableCloudSave(cloud, loadFromCloud: false);
        return (manager, local, cloud);
    }

    public static async Task<int> Main()
    {
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine("Production-source logic checks; Unity/scene/cloud boundaries substituted.");

        await Run("TTL boundary retains packet ownership and reports failure once", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            var packets = f.Start();
            f.Manager.TransferPacket(packets[0], PacketLocation.NodeInput, "router");
            f.Tick(0.999f);
            Require(f.Outcomes.Count == 0, "must not expire before deadline");
            f.Tick(1f);
            Require(f.Outcomes.Count == 1 && f.Dropped == 1, "deadline must report one failure");
            Require(f.Outcomes[0].result == TrafficStatResult.Failed, "outcome must be failure");
            Require(f.Manager.ActivePacketCount == 3 && f.Pool.LeasedPacketCount == 3, "TTL must retain packets");
            Require(packets[0].Location == PacketLocation.NodeInput && packets[0].OwnerID == "router", "ownership must survive TTL");
            f.Tick(2f);
            Require(f.Outcomes.Count == 1, "later ticks must not repeat failure");
            f.Manager.ReportPacketArrived(packets[0]);
            f.Manager.ReportPacketDropped(packets[1], "capacity");
            Require(f.Manager.ActiveTrafficCount == 1, "partial resolution must retain traffic");
            f.Manager.ReportPacketArrived(packets[2]);
            Require(f.Manager.ActiveTrafficCount == 0 && f.Manager.ActivePacketCount == 0, "all resolved must release traffic and packets");
            Require(f.Pool.LeasedTrafficCount == 0 && f.Pool.LeasedPacketCount == 0, "pool leases must be returned");
            Require(f.Outcomes.Count == 1 && f.Delivered == 0 && f.Completed == 0, "late arrival must not change expired outcome to success");
        }));

        await Run("All arrivals report success only after last packet", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            var packets = f.Start(ttl: 10f);
            Require(Math.Abs(packets.Sum(p => p.size) - 10.5f) < 0.0001f, "packet sizes must preserve fractional total");
            f.Manager.ReportPacketArrived(packets[0]);
            f.Manager.ReportPacketArrived(packets[1]);
            Require(f.Outcomes.Count == 0 && f.Manager.ActiveTrafficCount == 1, "no early completion");
            f.Manager.ReportPacketArrived(packets[2]);
            Require(f.Outcomes.Count == 1 && f.Delivered == 1 && f.Completed == 1, "one successful completion");
            Require(f.Outcomes[0].sizeMB == 10.5f && f.Outcomes[0].result == TrafficStatResult.Delivered, "correct success statistics");
            Require(f.Pool.LeasedPacketCount == 0 && f.Pool.LeasedTrafficCount == 0, "all leases returned");
        }));

        await Run("Mixed arrival and drop produces one failed traffic", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            var packets = f.Start();
            f.Manager.ReportPacketDropped(packets[0], "queue full");
            f.Manager.ReportPacketArrived(packets[1]);
            f.Manager.ReportPacketArrived(packets[2]);
            Require(f.Outcomes.Count == 1 && f.Dropped == 1 && f.Delivered == 0, "mixed result must fail once");
            Require(f.Outcomes[0].reason == "queue full", "failure reason retained");
            Require(f.Completed == 0 && f.Pool.LeasedPacketCount == 0, "no success callback and no packet lease");
        }));

        await Run("Duplicate arrival and drop do not double-release", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            var packet = f.Start(count: 1)[0];
            f.Manager.ReportPacketArrived(packet);
            f.Manager.ReportPacketArrived(packet);
            f.Manager.ReportPacketDropped(packet, "duplicate");
            Require(f.Outcomes.Count == 1 && f.Completed == 1, "duplicate reports must be ignored");
            Require(f.Pool.LeasedPacketCount == 0 && f.Manager.ActivePacketCount == 0, "lease counts must not underflow");
        }));

        await Run("Throwing event subscriber does not block cleanup or other subscribers", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            var packet = f.Start(count: 1)[0];
            int observed = 0;
            Action<PacketDropInfo> bad = _ => throw new InvalidOperationException("injected subscriber failure");
            Action<PacketDropInfo> good = _ => observed++;
            NetworkEventBus.OnPacketDropped += bad;
            NetworkEventBus.OnPacketDropped += good;
            try { f.Manager.ReportPacketDropped(packet, "test"); }
            finally
            {
                NetworkEventBus.OnPacketDropped -= bad;
                NetworkEventBus.OnPacketDropped -= good;
            }
            Require(observed == 1, "second subscriber must still run");
            Require(f.Pool.LeasedPacketCount == 0 && f.Pool.LeasedTrafficCount == 0, "cleanup must complete after subscriber exception");
            Require(f.Outcomes.Count == 1, "traffic must still finalize");
        }));

        await Run("Zero TTL does not expire", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            var packet = f.Start(ttl: 0f, count: 1)[0];
            f.Tick(10000f);
            Require(f.Outcomes.Count == 0 && f.Manager.ActivePacketCount == 1, "zero TTL disables expiry");
            f.Manager.ReportPacketArrived(packet);
            Require(f.Delivered == 1, "non-expiring traffic can succeed");
        }));

        await Run("Invalid request allocates no objects", () => Sync(() =>
        {
            using var f = new TrafficFixture();
            Require(f.Start(size: 0f) == null, "zero size rejected");
            Require(f.Start(count: 0) == null, "zero count rejected");
            Require(f.Manager.ActiveTrafficCount == 0 && f.Pool.LeasedPacketCount == 0 && f.Pool.LeasedTrafficCount == 0, "no leases for invalid request");
        }));

        await Run("Local save precedes cloud acknowledgement", async () =>
        {
            var f = SaveFixture();
            f.Manager.Data.maxUnlockedStageIndex = 7;
            var task = f.Manager.SaveAsync();
            Require(!task.IsCompleted, "cloud write must still be pending");
            Require(f.Local.Read().maxUnlockedStageIndex == 7 && f.Local.Read().hasPendingCloudSave, "local data and pending must persist before acknowledgement");
            Require(!JsonUtility.FromJson<GameData>(f.Cloud.Writes[0].Json).hasPendingCloudSave, "cloud payload excludes local pending state");
            f.Cloud.Succeed(0);
            await task;
            Require(!f.Local.Read().hasPendingCloudSave, "latest success clears pending");
        });

        await Run("Old upload success cannot clear newer pending save", async () =>
        {
            var f = SaveFixture();
            var first = f.Manager.SaveAsync();
            f.Manager.Data.maxUnlockedStageIndex = 9;
            var second = f.Manager.SaveAsync();
            f.Cloud.Succeed(0);
            await first;
            Require(f.Local.Read().hasPendingCloudSave, "old success must not clear newer pending");
            Require(f.Local.Read().maxUnlockedStageIndex == 9, "latest local progress retained");
            f.Cloud.Succeed(1);
            await second;
            Require(!f.Local.Read().hasPendingCloudSave, "latest success clears pending");
        });

        await Run("Old upload failure cannot re-mark completed newer save", async () =>
        {
            var f = SaveFixture();
            var first = f.Manager.SaveAsync();
            var second = f.Manager.SaveAsync();
            f.Cloud.Succeed(1);
            await second;
            f.Cloud.Fail(0);
            await first;
            Require(!f.Local.Read().hasPendingCloudSave, "old failure must not change latest completed status");
        });

        await Run("Latest upload failure retains local data for retry", async () =>
        {
            var f = SaveFixture();
            f.Manager.Data.maxUnlockedStageIndex = 12;
            var task = f.Manager.SaveAsync();
            f.Cloud.Fail(0);
            await task;
            Require(f.Local.Read().hasPendingCloudSave && f.Local.Read().maxUnlockedStageIndex == 12, "failure must retain progress and retry state");
        });

        await Run("Cloud read failure preserves local progress", async () =>
        {
            var f = SaveFixture();
            f.Manager.DisableCloudSave();
            f.Manager.Data.maxUnlockedStageIndex = 5;
            f.Manager.Save();
            f.Cloud.FailReads = true;
            f.Manager.EnableCloudSave(f.Cloud);
            await f.Manager.WaitForCloudLoadAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Require(f.Manager.Data.maxUnlockedStageIndex == 5 && f.Local.Read().maxUnlockedStageIndex == 5, "read failure must preserve local progress");
            Require(f.Manager.IsCloudLoadCompleted, "read failure must finish initialization");
        });

        await Run("Persisted pending save is retried on cloud enable", async () =>
        {
            var f = SaveFixture();
            var first = f.Manager.SaveAsync();
            f.Cloud.Fail(0);
            await first;
            var manager = new SaveManager();
            manager.SetStorage(f.Local);
            var cloud = new ControlledCloud();
            manager.EnableCloudSave(cloud);
            Require(cloud.Writes.Count == 1 && manager.Data.hasPendingCloudSave, "persisted pending must start retry");
            cloud.Succeed(0);
            await manager.WaitForCloudLoadAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Require(!f.Local.Read().hasPendingCloudSave, "retry success clears pending when no newer save exists");
        });

        await Run("Retry acknowledgement must not clear a newer pending save", async () =>
        {
            var f = SaveFixture();
            var initial = f.Manager.SaveAsync();
            f.Cloud.Fail(0);
            await initial;
            var manager = new SaveManager();
            manager.SetStorage(f.Local);
            var cloud = new ControlledCloud();
            manager.EnableCloudSave(cloud);
            manager.Data.maxUnlockedStageIndex = 20;
            var newer = manager.SaveAsync();
            cloud.Succeed(0);
            bool pendingAfterOldRetry = f.Local.Read().hasPendingCloudSave;
            cloud.Fail(1);
            await newer;
            await manager.WaitForCloudLoadAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Require(pendingAfterOldRetry, "old retry success cleared pending while newer upload was unresolved");
        });

        await Run("Old retry failure must not re-mark a completed newer save", async () =>
        {
            var f = SaveFixture();
            var initial = f.Manager.SaveAsync();
            f.Cloud.Fail(0);
            await initial;
            var manager = new SaveManager();
            manager.SetStorage(f.Local);
            var cloud = new ControlledCloud();
            manager.EnableCloudSave(cloud);
            manager.Data.maxUnlockedStageIndex = 21;
            var newer = manager.SaveAsync();
            cloud.Succeed(1);
            await newer;
            cloud.Fail(0);
            await manager.WaitForCloudLoadAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Require(!f.Local.Read().hasPendingCloudSave, "old retry failure must preserve completed newer save status");
            Require(f.Local.Read().maxUnlockedStageIndex == 21, "newer progress must be preserved");
        });

        Console.WriteLine($"RESULT: {passed} passed, {failed} failed, {assertions} assertions evaluated");
        return failed == 0 ? 0 : 1;
    }
}
