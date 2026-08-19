using Grpc.Core;
using TidsOS.Agent.Storage;
using TidsOS.Contracts.Storage;

namespace TidsOS.Agent.Services;

public sealed class ChunkStoreServiceImpl(LocalChunkStore store, ILogger<ChunkStoreServiceImpl> logger)
    : ChunkStore.ChunkStoreBase
{
    public override Task<PutChunkResponse> PutChunk(PutChunkRequest request, ServerCallContext context)
    {
        var (accepted, hashVerified) = store.Put(request.ChunkHash, request.Content.Memory);
        if (!hashVerified)
        {
            logger.LogWarning("Rejected chunk {ChunkHash}: claimed hash did not match content", request.ChunkHash);
            return Task.FromResult(new PutChunkResponse
            {
                Accepted = false,
                HashVerified = false,
                Message = "hash mismatch",
            });
        }

        return Task.FromResult(new PutChunkResponse
        {
            Accepted = accepted,
            HashVerified = true,
            Message = "stored",
        });
    }

    public override Task<GetChunkResponse> GetChunk(GetChunkRequest request, ServerCallContext context)
    {
        if (!store.TryGet(request.ChunkHash, out var content))
        {
            return Task.FromResult(new GetChunkResponse { Found = false });
        }

        return Task.FromResult(new GetChunkResponse
        {
            Found = true,
            Content = Google.Protobuf.ByteString.CopyFrom(content),
        });
    }
}
