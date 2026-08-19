extern alias AgentHost;

using Grpc.Net.Client;
using Google.Protobuf;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TidsOS.Agent.Storage;
using TidsOS.Contracts.Storage;
using Xunit;
using AgentProgram = AgentHost::Program;

namespace TidsOS.Tests;

// Verifies the RFC-0002 ChunkStore wire protocol end to end, in-process,
// hosted by the Agent itself (it's the server side for chunk data, unlike
// RFC-0001 where the Agent was purely a client).
public class ChunkStoreGrpcTests : IClassFixture<WebApplicationFactory<AgentProgram>>, IDisposable
{
    private readonly string _tempChunkDir = Directory.CreateTempSubdirectory("tidsos-chunkstore-grpc-tests-").FullName;
    private readonly WebApplicationFactory<AgentProgram> _factory;

    public ChunkStoreGrpcTests(WebApplicationFactory<AgentProgram> factory)
    {
        var tempChunkDir = _tempChunkDir;
        // Override Program.cs's real-per-OS LocalChunkStore registration with
        // one pointed at a throwaway temp directory, so tests don't write
        // into the developer/CI machine's actual %LOCALAPPDATA%.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(new LocalChunkStore(tempChunkDir))));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempChunkDir))
        {
            Directory.Delete(_tempChunkDir, recursive: true);
        }
    }

    private ChunkStore.ChunkStoreClient CreateClient()
    {
        var handler = new ResponseVersionHandler { InnerHandler = _factory.Server.CreateHandler() };
        var channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = handler,
        });
        return new ChunkStore.ChunkStoreClient(channel);
    }

    [Fact]
    public async Task PutChunk_then_GetChunk_round_trips_the_content()
    {
        var client = CreateClient();
        var content = "hello over the wire"u8.ToArray();
        var hash = LocalChunkStore.ComputeHash(content);

        var putResponse = await client.PutChunkAsync(new PutChunkRequest
        {
            ChunkHash = hash,
            Content = ByteString.CopyFrom(content),
        });

        Assert.True(putResponse.Accepted);
        Assert.True(putResponse.HashVerified);

        var getResponse = await client.GetChunkAsync(new GetChunkRequest { ChunkHash = hash });

        Assert.True(getResponse.Found);
        Assert.Equal(content, getResponse.Content.ToByteArray());
    }

    [Fact]
    public async Task PutChunk_rejects_a_hash_that_does_not_match_the_content()
    {
        var client = CreateClient();
        var content = "some content"u8.ToArray();
        var wrongHash = LocalChunkStore.ComputeHash("different content"u8.ToArray());

        var response = await client.PutChunkAsync(new PutChunkRequest
        {
            ChunkHash = wrongHash,
            Content = ByteString.CopyFrom(content),
        });

        Assert.False(response.Accepted);
        Assert.False(response.HashVerified);
    }

    [Fact]
    public async Task GetChunk_for_a_hash_never_written_is_not_found()
    {
        var client = CreateClient();
        var neverWrittenHash = LocalChunkStore.ComputeHash(Guid.NewGuid().ToByteArray());

        var response = await client.GetChunkAsync(new GetChunkRequest { ChunkHash = neverWrittenHash });

        Assert.False(response.Found);
    }
}
