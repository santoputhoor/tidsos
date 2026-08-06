using TidsOS.Controller.Registry;

namespace TidsOS.Controller.Services;

// Nodes in tidsOS are expected to disappear — laptops sleep, Wi-Fi drops,
// someone shuts down at 5pm. A single missed heartbeat is normal, not a
// failure. This reaper only marks a node offline once it has missed several
// heartbeat intervals in a row, and logs the transition so operators (and
// eventually the scheduler, RFC-0004) can react.
public sealed class StaleNodeReaper(NodeRegistry registry, ILogger<StaleNodeReaper> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30); // ~3x the heartbeat interval

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var node in registry.MarkStale(StaleAfter))
            {
                logger.LogWarning(
                    "Node {NodeId} ({Hostname}) went offline — no heartbeat for over {StaleSeconds}s",
                    node.NodeId, node.Hostname, StaleAfter.TotalSeconds);
            }
        }
    }
}
