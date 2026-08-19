extern alias ControllerHost;

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TidsOS.Contracts.Node;
using TidsOS.Controller.Registry;
using Xunit;
using ControllerProgram = ControllerHost::Program;

namespace TidsOS.Tests;

// Verifies the actual RFC-0001 wire protocol (Register + Heartbeat) end to
// end, in-process via WebApplicationFactory<Program>. Because this hosts the
// real Controller pipeline over an in-memory transport rather than real OS
// sockets, it runs identically on Windows, Linux, and macOS in CI — the
// Kestrel dual-port h2c setup that only real sockets need doesn't come into
// play here.
public class NodeRegistrationGrpcTests : IClassFixture<WebApplicationFactory<ControllerProgram>>
{
    private readonly WebApplicationFactory<ControllerProgram> _factory;

    public NodeRegistrationGrpcTests(WebApplicationFactory<ControllerProgram> factory)
    {
        _factory = factory;
    }

    private NodeService.NodeServiceClient CreateClient()
    {
        var handler = new ResponseVersionHandler { InnerHandler = _factory.Server.CreateHandler() };
        var channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = handler,
        });
        return new NodeService.NodeServiceClient(channel);
    }

    [Fact]
    public async Task Register_is_accepted_and_the_node_appears_in_the_registry()
    {
        var client = CreateClient();
        var nodeId = Guid.NewGuid().ToString("N");

        var response = await client.RegisterAsync(new RegisterRequest
        {
            NodeId = nodeId,
            Hostname = "test-node",
            OsDescription = "test-os",
            CpuCores = 4,
            TotalMemoryMb = 8192,
            AgentVersion = "0.1.0-test",
        });

        Assert.True(response.Accepted);
        Assert.Equal(nodeId, response.NodeId);
        Assert.True(response.HeartbeatIntervalSeconds > 0);

        var registry = _factory.Services.GetRequiredService<NodeRegistry>();
        Assert.Contains(registry.Snapshot(), n => n.NodeId == nodeId);
    }

    [Fact]
    public async Task Heartbeat_stream_after_register_updates_the_registry()
    {
        var client = CreateClient();
        var nodeId = Guid.NewGuid().ToString("N");

        await client.RegisterAsync(new RegisterRequest
        {
            NodeId = nodeId,
            Hostname = "test-node",
            OsDescription = "test-os",
            CpuCores = 2,
            TotalMemoryMb = 4096,
            AgentVersion = "0.1.0-test",
        });

        using var call = client.Heartbeat();
        await call.RequestStream.WriteAsync(new HeartbeatRequest
        {
            NodeId = nodeId,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CpuUsagePercent = 12.5,
            MemoryAvailableMb = 1024,
            Status = NodeStatus.Online,
        });
        await call.RequestStream.CompleteAsync();

        var acks = new List<HeartbeatAck>();
        await foreach (var ack in call.ResponseStream.ReadAllAsync())
        {
            acks.Add(ack);
        }

        Assert.Single(acks);
        Assert.True(acks[0].Acknowledged);

        var registry = _factory.Services.GetRequiredService<NodeRegistry>();
        var record = Assert.Single(registry.Snapshot(), n => n.NodeId == nodeId);
        Assert.Equal(12.5, record.LastCpuUsagePercent);
    }

    [Fact]
    public async Task Heartbeat_from_a_node_that_never_registered_is_not_acknowledged()
    {
        var client = CreateClient();

        using var call = client.Heartbeat();
        await call.RequestStream.WriteAsync(new HeartbeatRequest
        {
            NodeId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CpuUsagePercent = 0,
            MemoryAvailableMb = 0,
            Status = NodeStatus.Online,
        });
        await call.RequestStream.CompleteAsync();

        var acks = new List<HeartbeatAck>();
        await foreach (var ack in call.ResponseStream.ReadAllAsync())
        {
            acks.Add(ack);
        }

        Assert.Single(acks);
        Assert.False(acks[0].Acknowledged);
    }
}
