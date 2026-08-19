extern alias ControllerHost;

using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TidsOS.Contracts.Storage;
using TidsOS.Controller.Registry;
using Xunit;
using ControllerProgram = ControllerHost::Program;

namespace TidsOS.Tests;

// Verifies the RFC-0002 placement wire protocol end to end, in-process, the
// same way NodeRegistrationGrpcTests verifies RFC-0001.
public class PlacementServiceGrpcTests : IClassFixture<WebApplicationFactory<ControllerProgram>>
{
    private const string SampleHash = "ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34ab12cd34";

    private readonly WebApplicationFactory<ControllerProgram> _factory;

    public PlacementServiceGrpcTests(WebApplicationFactory<ControllerProgram> factory)
    {
        _factory = factory;
    }

    private PlacementService.PlacementServiceClient CreateClient()
    {
        var handler = new ResponseVersionHandler { InnerHandler = _factory.Server.CreateHandler() };
        var channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = handler,
        });
        return new PlacementService.PlacementServiceClient(channel);
    }

    private static string NewNodeId() => Guid.NewGuid().ToString("N");

    [Fact]
    public async Task RequestPlacement_offers_registered_placeable_nodes()
    {
        var nodeRegistry = _factory.Services.GetRequiredService<NodeRegistry>();
        var nodeId = NewNodeId();
        nodeRegistry.Register(nodeId, "test-node", "test-os", 4, 8000, "0.1.0-test", "test-node:5280");

        var response = await CreateClient().RequestPlacementAsync(new RequestPlacementRequest
        {
            ChunkHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            SizeBytes = 1024,
        });

        Assert.Contains(response.Targets, t => t.NodeId == nodeId && t.ChunkStoreAddress == "test-node:5280");
        Assert.True(response.WriteQuorum >= 1);
    }

    [Fact]
    public async Task ConfirmWrite_then_GetChunkLocations_reflects_the_confirmed_replica()
    {
        var nodeRegistry = _factory.Services.GetRequiredService<NodeRegistry>();
        var nodeId = NewNodeId();
        nodeRegistry.Register(nodeId, "test-node-2", "test-os", 4, 8000, "0.1.0-test", "test-node-2:5280");
        var client = CreateClient();
        var chunkHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        await client.RequestPlacementAsync(new RequestPlacementRequest { ChunkHash = chunkHash, SizeBytes = 512 });
        var confirmResponse = await client.ConfirmWriteAsync(new ConfirmWriteRequest
        {
            ChunkHash = chunkHash,
            NodeIds = { nodeId },
        });

        Assert.True(confirmResponse.Accepted);

        var locations = await client.GetChunkLocationsAsync(new GetChunkLocationsRequest { ChunkHash = chunkHash });
        Assert.Contains(locations.Locations, l => l.NodeId == nodeId);
    }

    [Fact]
    public async Task ConfirmWrite_for_a_chunk_that_was_never_placed_is_not_accepted()
    {
        var client = CreateClient();

        var response = await client.ConfirmWriteAsync(new ConfirmWriteRequest
        {
            ChunkHash = "never-requested-" + Guid.NewGuid().ToString("N"),
            NodeIds = { NewNodeId() },
        });

        Assert.False(response.Accepted);
    }

    [Fact]
    public async Task GetChunkLocations_for_an_unknown_chunk_returns_no_locations()
    {
        var client = CreateClient();

        var response = await client.GetChunkLocationsAsync(
            new GetChunkLocationsRequest { ChunkHash = SampleHash + "-unused" });

        Assert.Empty(response.Locations);
    }
}
