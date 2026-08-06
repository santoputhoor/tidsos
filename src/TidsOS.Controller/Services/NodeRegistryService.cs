using Grpc.Core;
using TidsOS.Contracts.Node;
using TidsOS.Controller.Registry;

namespace TidsOS.Controller.Services;

public sealed class NodeRegistryService(NodeRegistry registry, ILogger<NodeRegistryService> logger)
    : NodeService.NodeServiceBase
{
    private const int HeartbeatIntervalSeconds = 10;

    public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        registry.Register(
            request.NodeId,
            request.Hostname,
            request.OsDescription,
            request.CpuCores,
            request.TotalMemoryMb,
            request.AgentVersion);

        logger.LogInformation(
            "Node registered: {NodeId} ({Hostname}, {CpuCores} cores, {MemoryMb} MB, agent {AgentVersion})",
            request.NodeId, request.Hostname, request.CpuCores, request.TotalMemoryMb, request.AgentVersion);

        return Task.FromResult(new RegisterResponse
        {
            Accepted = true,
            NodeId = request.NodeId,
            HeartbeatIntervalSeconds = HeartbeatIntervalSeconds,
            Message = "welcome to tidsOS",
        });
    }

    public override async Task Heartbeat(
        IAsyncStreamReader<HeartbeatRequest> requestStream,
        IServerStreamWriter<HeartbeatAck> responseStream,
        ServerCallContext context)
    {
        await foreach (var beat in requestStream.ReadAllAsync(context.CancellationToken))
        {
            var known = registry.RecordHeartbeat(beat.NodeId, beat.CpuUsagePercent, beat.MemoryAvailableMb);
            if (!known)
            {
                logger.LogWarning("Heartbeat from unknown node {NodeId}; ignoring until it re-registers", beat.NodeId);
            }

            await responseStream.WriteAsync(new HeartbeatAck
            {
                Acknowledged = known,
                ServerTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
        }
    }
}
