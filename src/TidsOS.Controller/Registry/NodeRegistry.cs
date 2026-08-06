using System.Collections.Concurrent;

namespace TidsOS.Controller.Registry;

// In-memory registry for the MVP. Real deployments will need this durable
// (RFC-0002, distributed storage) so a controller restart doesn't forget the
// fleet, but for proving out node registration/liveness this is enough.
public sealed class NodeRegistry
{
    private readonly ConcurrentDictionary<string, NodeRecord> _nodes = new();

    public NodeRecord Register(string nodeId, string hostname, string osDescription, int cpuCores, long totalMemoryMb, string agentVersion)
    {
        var now = DateTimeOffset.UtcNow;
        return _nodes.AddOrUpdate(
            nodeId,
            _ => new NodeRecord
            {
                NodeId = nodeId,
                Hostname = hostname,
                OsDescription = osDescription,
                CpuCores = cpuCores,
                TotalMemoryMb = totalMemoryMb,
                AgentVersion = agentVersion,
                RegisteredAt = now,
                LastHeartbeatAt = now,
                IsOnline = true,
            },
            (_, existing) =>
            {
                existing.LastHeartbeatAt = now;
                existing.IsOnline = true;
                return existing;
            });
    }

    public bool RecordHeartbeat(string nodeId, double cpuUsagePercent, long memoryAvailableMb)
    {
        if (!_nodes.TryGetValue(nodeId, out var record))
        {
            return false;
        }

        record.LastHeartbeatAt = DateTimeOffset.UtcNow;
        record.LastCpuUsagePercent = cpuUsagePercent;
        record.LastMemoryAvailableMb = memoryAvailableMb;
        record.IsOnline = true;
        return true;
    }

    public IReadOnlyCollection<NodeRecord> Snapshot() => _nodes.Values.ToList();

    public IEnumerable<NodeRecord> MarkStale(TimeSpan staleAfter)
    {
        var cutoff = DateTimeOffset.UtcNow - staleAfter;
        foreach (var record in _nodes.Values)
        {
            if (record.IsOnline && record.LastHeartbeatAt < cutoff)
            {
                record.IsOnline = false;
                yield return record;
            }
        }
    }
}
