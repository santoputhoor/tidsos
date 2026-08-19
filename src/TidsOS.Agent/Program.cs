using Microsoft.AspNetCore.Server.Kestrel.Core;
using TidsOS.Agent;
using TidsOS.Agent.Services;
using TidsOS.Agent.Storage;

var builder = WebApplication.CreateBuilder(args);

// The agent is both a client (registers/heartbeats out to the Controller,
// unchanged from RFC-0001) and, as of RFC-0002, a server: it hosts a
// ChunkStore so other nodes can push/pull chunks directly to/from it. Same
// h2c-needs-its-own-port constraint the Controller has.
var chunkStorePort = builder.Configuration.GetValue("TIDSOS_CHUNKSTORE_PORT", 5280);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(chunkStorePort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc(options =>
{
    // Default gRPC message-size limits (4 MiB) are too small for RFC-0002's
    // proposed 8 MiB chunk size; headroom set above that.
    options.MaxReceiveMessageSize = 16 * 1024 * 1024;
    options.MaxSendMessageSize = 16 * 1024 * 1024;
});

builder.Services.AddSingleton(new LocalChunkStore());
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGrpcService<ChunkStoreServiceImpl>();

app.Run();

// Exposed so the test project's WebApplicationFactory<Program> can host this
// app in-process to verify the RFC-0002 ChunkStore wire protocol identically
// on every CI runner OS.
public partial class Program;
