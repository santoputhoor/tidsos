using System.Collections.Concurrent;

namespace TidsOS.Controller.Registry;

// The Controller's view of "which nodes hold which chunks" (RFC-0002). Same
// pragmatic in-memory starting point as NodeRegistry — durability is an
// open question left to a future RFC, not solved here.
public sealed class ChunkRegistry
{
    private readonly ConcurrentDictionary<string, ChunkRecord> _chunks = new();

    public ChunkRecord GetOrCreate(string chunkHash, long sizeBytes) =>
        _chunks.GetOrAdd(chunkHash, hash => new ChunkRecord { ChunkHash = hash, SizeBytes = sizeBytes });

    public bool TryGet(string chunkHash, out ChunkRecord? record) => _chunks.TryGetValue(chunkHash, out record);

    // Returns false if the chunk was never placed via RequestPlacement — a
    // caller can't will a brand new chunk into existence just by confirming it.
    public bool ConfirmWrite(string chunkHash, IEnumerable<string> nodeIds)
    {
        if (!_chunks.TryGetValue(chunkHash, out var record))
        {
            return false;
        }

        foreach (var nodeId in nodeIds)
        {
            record.ReplicaNodeIds.TryAdd(nodeId, 0);
        }

        return true;
    }

    // A node has been presumed lost (RFC-0002) — strike it from every chunk
    // it was holding and report which chunks are now under-replicated as a
    // result, so the reconciler can queue repair.
    public IReadOnlyCollection<string> RemoveNodeFromAllChunks(string nodeId)
    {
        var affected = new List<string>();
        foreach (var record in _chunks.Values)
        {
            if (record.ReplicaNodeIds.TryRemove(nodeId, out _))
            {
                affected.Add(record.ChunkHash);
            }
        }

        return affected;
    }

    public IReadOnlyCollection<ChunkRecord> Snapshot() => _chunks.Values.ToList();
}
