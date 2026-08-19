using System.Collections.Concurrent;

namespace TidsOS.Controller.Registry;

// In-memory registry for the MVP. A Controller restart forgetting the fleet
// (and, since RFC-0002, the storage placement map) is a known gap — see both
// RFCs' "Unresolved questions" — but it's enough to prove out registration,
// liveness, and now placement.
public sealed class NodeRegistry
{
    private readonly ConcurrentDictionary<string, NodeRecord> _nodes = new();

    public NodeRecord Register(string nodeId, string hostname, string osDescription, int cpuCores, long totalMemoryMb, string agentVersion, string chunkStoreAddress = "")
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
                ChunkStoreAddress = chunkStoreAddress,
                RegisteredAt = now,
                LastHeartbeatAt = now,
                IsOnline = true,
            },
            (_, existing) =>
            {
                existing.LastHeartbeatAt = now;
                existing.IsOnline = true;
                existing.IsPresumedLost = false; // it's back — RFC-0002 repair can stand down
                existing.ChunkStoreAddress = chunkStoreAddress;
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
        record.IsPresumedLost = false;
        return true;
    }

    public IReadOnlyCollection<NodeRecord> Snapshot() => _nodes.Values.ToList();

    // Nodes eligible to receive new chunk placements (RFC-0002): reachable
    // right now and actually advertising a ChunkStore endpoint.
    public IReadOnlyCollection<NodeRecord> PlaceableNodes() =>
        _nodes.Values
            .Where(n => n.IsOnline && !n.IsPresumedLost && !string.IsNullOrEmpty(n.ChunkStoreAddress))
            .ToList();

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

    // RFC-0002: a much longer-fused signal than MarkStale. A node crossing
    // this threshold is when the storage layer should stop waiting for it
    // to come back and start re-replicating what it held.
    public IEnumerable<NodeRecord> MarkPresumedLost(TimeSpan presumedLostAfter)
    {
        var cutoff = DateTimeOffset.UtcNow - presumedLostAfter;
        foreach (var record in _nodes.Values)
        {
            if (!record.IsPresumedLost && record.LastHeartbeatAt < cutoff)
            {
                record.IsPresumedLost = true;
                yield return record;
            }
        }
    }
}
