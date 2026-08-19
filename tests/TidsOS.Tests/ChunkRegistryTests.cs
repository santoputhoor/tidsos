using TidsOS.Controller.Registry;
using Xunit;

namespace TidsOS.Tests;

public class ChunkRegistryTests
{
    private const string SampleHash = "ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34";

    [Fact]
    public void GetOrCreate_returns_the_same_record_on_repeated_calls()
    {
        var registry = new ChunkRegistry();

        var first = registry.GetOrCreate(SampleHash, sizeBytes: 1024);
        var second = registry.GetOrCreate(SampleHash, sizeBytes: 1024);

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfirmWrite_fails_for_a_chunk_that_was_never_placed()
    {
        var registry = new ChunkRegistry();

        var accepted = registry.ConfirmWrite(SampleHash, ["node-a"]);

        Assert.False(accepted);
    }

    [Fact]
    public void ConfirmWrite_records_replicas_after_placement()
    {
        var registry = new ChunkRegistry();
        registry.GetOrCreate(SampleHash, sizeBytes: 1024);

        var accepted = registry.ConfirmWrite(SampleHash, ["node-a", "node-b"]);

        Assert.True(accepted);
        registry.TryGet(SampleHash, out var record);
        Assert.NotNull(record);
        Assert.Equal(2, record!.ReplicaNodeIds.Count);
    }

    [Fact]
    public void RemoveNodeFromAllChunks_strikes_the_node_and_reports_affected_chunks()
    {
        var registry = new ChunkRegistry();
        registry.GetOrCreate(SampleHash, sizeBytes: 1024);
        registry.ConfirmWrite(SampleHash, ["node-a", "node-b"]);

        var affected = registry.RemoveNodeFromAllChunks("node-a");

        Assert.Single(affected, SampleHash);
        registry.TryGet(SampleHash, out var record);
        Assert.DoesNotContain("node-a", record!.ReplicaNodeIds.Keys);
        Assert.Contains("node-b", record!.ReplicaNodeIds.Keys);
    }

    [Fact]
    public void RemoveNodeFromAllChunks_is_a_noop_for_a_node_holding_nothing()
    {
        var registry = new ChunkRegistry();
        registry.GetOrCreate(SampleHash, sizeBytes: 1024);
        registry.ConfirmWrite(SampleHash, ["node-a"]);

        var affected = registry.RemoveNodeFromAllChunks("node-that-never-had-anything");

        Assert.Empty(affected);
    }
}
