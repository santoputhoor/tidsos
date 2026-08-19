using TidsOS.Controller.Registry;

namespace TidsOS.Controller.Services;

// RFC-0002: watches for nodes crossing from "unreachable" into "presumed
// lost" and strikes them from the chunks they held, so GetChunkLocations
// stops offering dead replicas and the under-replication shows up in the
// /chunks status endpoint.
//
// What this deliberately does NOT do yet: automatically copy the affected
// chunks to new nodes to restore the replication factor. That requires
// orchestrating an agent-to-agent (or agent-to-agent-via-controller) data
// transfer, which is real follow-up work — for the MVP, under-replicated
// chunks are surfaced (via GetChunkLocations returning fewer than R
// locations, and via /chunks) rather than silently repaired. Flagging this
// gap explicitly rather than pretending it's solved, same as RFC-0001 did
// for its own known limitations.
public sealed class ReplicationReconciler(NodeRegistry nodeRegistry, ChunkRegistry chunkRegistry, ILogger<ReplicationReconciler> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    // RFC-0002 proposes 24h in production — deliberately configurable so
    // tests (and this constructor) don't have to wait a day to exercise it.
    public TimeSpan PresumedLostAfter { get; init; } = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var node in nodeRegistry.MarkPresumedLost(PresumedLostAfter))
            {
                var affectedChunks = chunkRegistry.RemoveNodeFromAllChunks(node.NodeId);
                logger.LogWarning(
                    "Node {NodeId} ({Hostname}) presumed lost after {Threshold} — {ChunkCount} chunk(s) now under-replicated",
                    node.NodeId, node.Hostname, PresumedLostAfter, affectedChunks.Count);
            }
        }
    }
}
