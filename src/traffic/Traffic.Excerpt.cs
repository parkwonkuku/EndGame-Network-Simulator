// 원본 발췌: Assets/Project/Scripts/Core/NetworkGraph/Traffic.cs
// 실제 프로젝트의 상태와 완료 조건을 유지했습니다.

public class Traffic
{
    public string id;
    public string startNodeID;
    public string destNodeID;
    public string resolvedServerID;
    public float totalDataSize;
    public int totalPacketCount;
    public TrafficKind kind;
    public TrafficState state;

    public int emittedPacketCount;
    public int arrivedPacketCount;
    public int droppedPacketCount;
    public float arrivedDataSize;
    public float droppedDataSize;
    public string firstFailureReason;

    public float createdAt;
    public float expiresAt;
    public bool outcomeReported;

    public Action onComplete;

    public bool IsResolved => arrivedPacketCount + droppedPacketCount >= totalPacketCount;
    public bool IsSuccessful => IsResolved && droppedPacketCount == 0;
    public string TargetID => !string.IsNullOrEmpty(resolvedServerID) ? resolvedServerID : destNodeID;

    public bool IsExpiredAt(float simulationTime)
        => expiresAt > 0f && simulationTime >= expiresAt;

    public void RecordArrived(float size)
    {
        arrivedPacketCount++;
        arrivedDataSize += size;
    }

    public void RecordDropped(float size, string reason)
    {
        droppedPacketCount++;
        droppedDataSize += size;
        if (string.IsNullOrEmpty(firstFailureReason))
            firstFailureReason = reason;
    }
}

public enum TrafficState
{
    Created,
    Emitting,
    InProgress,
    Completed,
    Failed,
    Expired
}
