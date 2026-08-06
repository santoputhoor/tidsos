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

    public DateTimeOffset LastHeartbeatAt { get; set; }
    public double LastCpuUsagePercent { get; set; }
    public long LastMemoryAvailableMb { get; set; }
    public bool IsOnline { get; set; } = true;
}
