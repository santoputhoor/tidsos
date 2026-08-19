namespace TidsOS.Controller.Registry;

public sealed class NodeRecord
{
    public required string NodeId { get; init; }
    public required string Hostname { get; init; }
    public required string OsDescription { get; init; }
    public required int CpuCores { get; init; }
    public required long TotalMemoryMb { get; init; }
    public required string AgentVersion { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }

    // host:port for this node's ChunkStore (RFC-0002). Empty if the node
    // isn't advertising storage participation (e.g. pre-RFC-0002 agents).
    public string ChunkStoreAddress { get; set; } = string.Empty;

    public DateTimeOffset LastHeartbeatAt { get; set; }
    public double LastCpuUsagePercent { get; set; }
    public long LastMemoryAvailableMb { get; set; }
    public bool IsOnline { get; set; } = true;

    // Separate, much longer-fused signal than IsOnline (RFC-0002): a node
    // being briefly unreachable doesn't mean its data needs re-replicating,
    // but a node presumed lost does.
    public bool IsPresumedLost { get; set; }
}
