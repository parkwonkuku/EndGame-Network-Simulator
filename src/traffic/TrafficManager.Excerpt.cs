// 검토용 재구성: Assets/Project/Scripts/Core/Managers/TrafficManager.cs
// 원본의 생성 실패 복원, 도착·드롭, TTL 만료와 최종화 분기를 유지했습니다.
// 클래스명과 필드를 정리하고 오디오, 개발 로그와 공개 API 오버로드를 생략했습니다.

public sealed class TrafficManagerExcerpt
{
    private readonly Dictionary<string, Traffic> activeTraffics = new();
    private readonly List<Traffic> expiredTrafficBuffer = new();
    private PacketLifecycleService packetLifecycle;

    private List<Packet> TryStartTraffic(
        string userID,
        string targetID,
        float trafficSize,
        int packetCount,
        TrafficKind kind,
        float ttlSeconds,
        Action onComplete)
    {
        if (trafficSize <= 0f || packetCount <= 0 || TrafficPoolManager.Instance == null)
            return null;

        float basePacketSize = Mathf.Floor(trafficSize / packetCount);
        float remainderSize = trafficSize - basePacketSize * packetCount;
        int wholeRemainderMB = Mathf.FloorToInt(remainderSize);
        float fractionalRemainderMB = remainderSize - wholeRemainderMB;

        Traffic traffic = TrafficPoolManager.Instance.GetTraffic();
        float createdAt = Simulation.Instance != null ? Simulation.Instance.GetCurrentTime() : 0f;
        traffic.Init(userID, targetID, trafficSize, packetCount, kind, createdAt, Mathf.Max(0f, ttlSeconds), onComplete);
        activeTraffics.Add(traffic.id, traffic);

        var packets = new List<Packet>(packetCount);
        for (int i = 0; i < packetCount; i++)
        {
            float currentPacketSize = basePacketSize;
            if (i < wholeRemainderMB)
                currentPacketSize += 1f;
            else if (i == wholeRemainderMB)
                currentPacketSize += fractionalRemainderMB;

            Packet packet = packetLifecycle.Create(traffic, currentPacketSize, userID);
            if (packet == null)
            {
                for (int j = 0; j < packets.Count; j++)
                    packetLifecycle.Release(packets[j]);
                activeTraffics.Remove(traffic.id);
                TrafficPoolManager.Instance.ReleaseTraffic(traffic);
                return null;
            }
            packets.Add(packet);
        }

        return packets;
    }

    public void ReportPacketArrived(Packet packet)
    {
        if (packetLifecycle == null || !packetLifecycle.IsActive(packet)) return;

        Traffic traffic = null;
        if (TryGetTraffic(packet.trafficID, out traffic))
            traffic.RecordArrived(packet.size);

        packetLifecycle.Release(packet);

        if (traffic != null)
            TryFinalizeTraffic(traffic);
    }

    public void ReportPacketDropped(Packet packet, string reason, Vector3? worldPosition = null)
    {
        if (packetLifecycle == null || !packetLifecycle.IsActive(packet)) return;

        Traffic traffic = null;
        if (TryGetTraffic(packet.trafficID, out traffic))
            traffic.RecordDropped(packet.size, reason);

        PacketDropInfo dropInfo = new(packet, reason, worldPosition);
        try
        {
            NetworkEventBus.RaisePacketDropped(dropInfo);
        }
        finally
        {
            packetLifecycle.Release(packet);
            if (traffic != null)
                TryFinalizeTraffic(traffic);
        }
    }

    private void TryFinalizeTraffic(Traffic traffic)
    {
        if (traffic == null || !traffic.IsResolved)
            return;

        if (!traffic.outcomeReported)
        {
            bool succeeded = traffic.IsSuccessful;
            traffic.state = succeeded ? TrafficState.Completed : TrafficState.Failed;
            PublishTrafficOutcome(traffic, succeeded);
        }

        ReleaseResolvedTraffic(traffic);
    }

    private void PublishTrafficOutcome(Traffic traffic, bool succeeded)
    {
        if (traffic == null || traffic.outcomeReported) return;

        traffic.outcomeReported = true;

        NetworkEventBus.RaiseTrafficStatRecorded(new TrafficStatRecord
        {
            trafficID = traffic.id,
            userID = traffic.startNodeID,
            targetID = traffic.TargetID,
            kind = traffic.kind,
            result = succeeded ? TrafficStatResult.Delivered : TrafficStatResult.Failed,
            sizeMB = traffic.totalDataSize,
            packetCount = traffic.totalPacketCount,
            reason = traffic.firstFailureReason
        });

        if (traffic.kind != TrafficKind.Normal) return;

        if (succeeded)
            NetworkEventBus.RaiseTrafficDelivered(traffic.totalDataSize);
        else
            NetworkEventBus.RaiseTrafficDropped(traffic.totalDataSize);
    }

    private void ReleaseResolvedTraffic(Traffic traffic)
    {
        if (traffic == null || !activeTraffics.Remove(traffic.id)) return;
        TrafficPoolManager.Instance?.ReleaseTraffic(traffic);
    }

    private void HandlePostTick(float _)
    {
        if (Simulation.Instance == null || activeTraffics.Count == 0) return;

        float simulationTime = Simulation.Instance.GetCurrentTime();
        expiredTrafficBuffer.Clear();

        foreach (Traffic traffic in activeTraffics.Values)
        {
            if (traffic.state != TrafficState.Expired &&
                !traffic.outcomeReported &&
                traffic.IsExpiredAt(simulationTime))
            {
                expiredTrafficBuffer.Add(traffic);
            }
        }

        for (int i = 0; i < expiredTrafficBuffer.Count; i++)
            ExpireTraffic(expiredTrafficBuffer[i]);

        expiredTrafficBuffer.Clear();
    }

    private void ExpireTraffic(Traffic traffic)
    {
        if (traffic == null || traffic.outcomeReported || traffic.IsResolved) return;

        traffic.state = TrafficState.Expired;
        if (string.IsNullOrEmpty(traffic.firstFailureReason))
            traffic.firstFailureReason = "트래픽 TTL 초과";

        PublishTrafficOutcome(traffic, false);
    }

    // TryGetTraffic과 이벤트 구독 코드는 생략했습니다.
}
