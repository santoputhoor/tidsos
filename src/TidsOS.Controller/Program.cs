using Microsoft.AspNetCore.Server.Kestrel.Core;
using TidsOS.Controller.Registry;
using TidsOS.Controller.Services;

var builder = WebApplication.CreateBuilder(args);

// Plain HTTP/2 (h2c) so agents on the LAN don't need a trusted cert to talk
// to the controller during local development / early deployments. Kestrel
// can't multiplex HTTP/1.1 and HTTP/2 on one unencrypted endpoint (that
// requires ALPN, which requires TLS), so gRPC and the human-readable status
// endpoints get separate ports.
var grpcPort = builder.Configuration.GetValue("TIDSOS_GRPC_PORT", 5270);
var httpPort = builder.Configuration.GetValue("TIDSOS_HTTP_PORT", 5271);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(grpcPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
    options.ListenAnyIP(httpPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
});

builder.Services.AddGrpc();
builder.Services.AddSingleton<NodeRegistry>();
builder.Services.AddHostedService<StaleNodeReaper>();

var app = builder.Build();

app.MapGrpcService<NodeRegistryService>();

app.MapGet("/", () => $"tidsOS controller is running. gRPC (NodeService) on port {grpcPort}, status API on port {httpPort}.");

app.MapGet("/nodes", (NodeRegistry registry) => registry.Snapshot().Select(n => new
{
    n.NodeId,
    n.Hostname,
    n.OsDescription,
    n.CpuCores,
    n.TotalMemoryMb,
    n.AgentVersion,
    n.IsOnline,
    n.LastCpuUsagePercent,
    n.LastMemoryAvailableMb,
    RegisteredAt = n.RegisteredAt,
    LastHeartbeatAt = n.LastHeartbeatAt,
}));

app.Run();

// Exposed so the test project's WebApplicationFactory<Program> can host this
// app in-process (real ASP.NET Core pipeline, no real sockets) to verify the
// RFC-0001 wire protocol identically on every CI runner OS.
public partial class Program;
