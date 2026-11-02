// 원본 발췌: Assets/Project/Scripts/Core/Managers/PacketLifecycleService.cs
// 실제 프로젝트의 패킷 생성, 소유권 확인과 풀 반환 코드를 유지했습니다.

public sealed class PacketLifecycleService
{
    private readonly Dictionary<string, Packet> activePackets = new();

    public int ActiveCount => activePackets.Count;

    public Packet Create(Traffic traffic, float size, string initialOwnerID)
    {
        if (traffic == null || TrafficPoolManager.Instance == null) return null;

        Packet packet = TrafficPoolManager.Instance.GetPacket();
        packet.Init(traffic.id, size, traffic.startNodeID, traffic.destNodeID, traffic.kind);
        packet.SetOwnership(PacketLocation.UserEmission, initialOwnerID);
        activePackets[packet.ID] = packet;
        return packet;
    }

    public bool IsActive(Packet packet)
        => packet != null &&
           !string.IsNullOrEmpty(packet.ID) &&
           activePackets.TryGetValue(packet.ID, out Packet registered) &&
           registered == packet;

    public void Transfer(Packet packet, PacketLocation location, string ownerID)
    {
        if (!IsActive(packet))
        {
            Debug.LogError("[PacketLifecycle] 등록되지 않은 패킷의 소유권 이전을 거부했습니다.");
            return;
        }

        packet.SetOwnership(location, ownerID);
    }

    public void Release(Packet packet)
    {
        if (!IsActive(packet)) return;

        activePackets.Remove(packet.ID);
        packet.SetOwnership(PacketLocation.Released, null);
        TrafficPoolManager.Instance?.ReleasePacket(packet);
    }
}
