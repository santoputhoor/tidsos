using Grpc.Core;
using TidsOS.Contracts.Storage;
using TidsOS.Controller.Registry;

namespace TidsOS.Controller.Services;

public sealed class PlacementServiceImpl(NodeRegistry nodeRegistry, ChunkRegistry chunkRegistry, ILogger<PlacementServiceImpl> logger)
    : PlacementService.PlacementServiceBase
{
    public override Task<RequestPlacementResponse> RequestPlacement(RequestPlacementRequest request, ServerCallContext context)
    {
        var existing = chunkRegistry.GetOrCreate(request.ChunkHash, request.SizeBytes);
        var candidates = nodeRegistry.PlaceableNodes();

        // Prefer nodes that don't already hold this chunk (RFC-0002 §Placement) —
        // avoids correlated loss from double-placing the same chunk on one node.
        var fresh = candidates.Where(n => !existing.ReplicaNodeIds.ContainsKey(n.NodeId)).ToList();
        var targets = fresh
            .Concat(candidates.Where(n => existing.ReplicaNodeIds.ContainsKey(n.NodeId)))
            .Take(StorageDefaults.ReplicationFactor)
            .ToList();

        if (targets.Count < StorageDefaults.ReplicationFactor)
        {
            logger.LogWarning(
                "Only {Available} node(s) available to place chunk {ChunkHash}; wanted {Wanted}",
                targets.Count, request.ChunkHash, StorageDefaults.ReplicationFactor);
        }

        var response = new RequestPlacementResponse
        {
            WriteQuorum = Math.Min(StorageDefaults.WriteQuorum, Math.Max(targets.Count, 1)),
        };
        response.Targets.AddRange(targets.Select(n => new NodeEndpoint
        {
            NodeId = n.NodeId,
            ChunkStoreAddress = n.ChunkStoreAddress,
        }));

        return Task.FromResult(response);
    }

    public override Task<ConfirmWriteResponse> ConfirmWrite(ConfirmWriteRequest request, ServerCallContext context)
    {
        var accepted = chunkRegistry.ConfirmWrite(request.ChunkHash, request.NodeIds);
        if (accepted)
        {
            logger.LogInformation(
                "Chunk {ChunkHash} confirmed on {Count} node(s)", request.ChunkHash, request.NodeIds.Count);
        }
        else
        {
            logger.LogWarning(
                "ConfirmWrite for unknown chunk {ChunkHash} — RequestPlacement must be called first", request.ChunkHash);
        }

        return Task.FromResult(new ConfirmWriteResponse { Accepted = accepted });
    }

    public override Task<GetChunkLocationsResponse> GetChunkLocations(GetChunkLocationsRequest request, ServerCallContext context)
    {
        var response = new GetChunkLocationsResponse();
        if (!chunkRegistry.TryGet(request.ChunkHash, out var record) || record is null)
        {
            return Task.FromResult(response);
        }

        var nodesById = nodeRegistry.Snapshot().ToDictionary(n => n.NodeId);
        foreach (var nodeId in record.ReplicaNodeIds.Keys)
        {
            if (nodesById.TryGetValue(nodeId, out var node) && !string.IsNullOrEmpty(node.ChunkStoreAddress))
            {
                response.Locations.Add(new NodeEndpoint { NodeId = nodeId, ChunkStoreAddress = node.ChunkStoreAddress });
            }
        }

        return Task.FromResult(response);
    }
}
