using TidsOS.Controller.Registry;
using Xunit;

namespace TidsOS.Tests;

// Pure unit tests for the RFC-0001 registry logic — no hosting, no network,
// so these exercise identically on every OS in the CI matrix.
public class NodeRegistryTests
{
    private static string NewNodeId() => Guid.NewGuid().ToString("N");

    [Fact]
    public void Register_is_idempotent_for_the_same_node_id()
    {
        var registry = new NodeRegistry();
        var nodeId = NewNodeId();

        registry.Register(nodeId, "laptop-1", "Windows", 8, 16000, "1.0.0");
        registry.Register(nodeId, "laptop-1", "Windows", 8, 16000, "1.0.0");

        var snapshot = registry.Snapshot();
        Assert.Single(snapshot, n => n.NodeId == nodeId);
    }

    [Fact]
    public void Heartbeat_updates_last_heartbeat_and_vitals_for_a_known_node()
    {
        var registry = new NodeRegistry();
        var nodeId = NewNodeId();
        registry.Register(nodeId, "laptop-1", "Linux", 4, 8000, "1.0.0");

        var accepted = registry.RecordHeartbeat(nodeId, cpuUsagePercent: 42.5, memoryAvailableMb: 2048);

        Assert.True(accepted);
        var record = Assert.Single(registry.Snapshot(), n => n.NodeId == nodeId);
        Assert.Equal(42.5, record.LastCpuUsagePercent);
        Assert.Equal(2048, record.LastMemoryAvailableMb);
        Assert.True(record.IsOnline);
    }

    [Fact]
    public void Heartbeat_from_an_unregistered_node_is_rejected()
    {
        var registry = new NodeRegistry();

        var accepted = registry.RecordHeartbeat(NewNodeId(), cpuUsagePercent: 1, memoryAvailableMb: 1);

        Assert.False(accepted);
    }

    [Fact]
    public void A_single_slow_heartbeat_does_not_mark_a_node_stale()
    {
        // RFC-0001's whole point: one missed beat is normal, not a failure.
        var registry = new NodeRegistry();
        var nodeId = NewNodeId();
        registry.Register(nodeId, "laptop-1", "macOS", 8, 16000, "1.0.0");

        var staleNodes = registry.MarkStale(TimeSpan.FromHours(1)).ToList();

        Assert.Empty(staleNodes);
        Assert.True(Assert.Single(registry.Snapshot(), n => n.NodeId == nodeId).IsOnline);
    }

    [Fact]
    public async Task A_node_with_no_recent_heartbeat_is_marked_stale_exactly_once()
    {
        var registry = new NodeRegistry();
        var nodeId = NewNodeId();
        registry.Register(nodeId, "laptop-1", "macOS", 8, 16000, "1.0.0");

        // Let real time pass so LastHeartbeatAt is provably older than the
        // (very short) threshold below — deterministic without mocking the clock.
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        var firstPass = registry.MarkStale(TimeSpan.FromMilliseconds(10)).ToList();
        Assert.Single(firstPass, n => n.NodeId == nodeId);
        Assert.False(Assert.Single(registry.Snapshot(), n => n.NodeId == nodeId).IsOnline);

        // Reaper runs on an interval — a node already marked offline shouldn't
        // be reported again on the next pass until it heartbeats back online.
        var secondPass = registry.MarkStale(TimeSpan.FromMilliseconds(10)).ToList();
        Assert.Empty(secondPass);
    }

    [Fact]
    public void A_heartbeat_after_going_stale_brings_the_node_back_online()
    {
        var registry = new NodeRegistry();
        var nodeId = NewNodeId();
        registry.Register(nodeId, "laptop-1", "Linux", 4, 8000, "1.0.0");
        registry.MarkStale(TimeSpan.Zero); // force offline

        registry.RecordHeartbeat(nodeId, cpuUsagePercent: 5, memoryAvailableMb: 1000);

        Assert.True(Assert.Single(registry.Snapshot(), n => n.NodeId == nodeId).IsOnline);
    }
}
