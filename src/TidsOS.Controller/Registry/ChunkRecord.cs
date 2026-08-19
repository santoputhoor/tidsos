using System.Collections.Concurrent;

namespace TidsOS.Controller.Registry;

public sealed class ChunkRecord
{
    public required string ChunkHash { get; init; }
    public required long SizeBytes { get; init; }

    // Node ids that have confirmed holding this chunk. A ConcurrentDictionary
    // used as a set — there's no ConcurrentHashSet<T> in the BCL.
    public ConcurrentDictionary<string, byte> ReplicaNodeIds { get; } = new();
}
