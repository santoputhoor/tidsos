using System.Runtime.InteropServices;
using Grpc.Core;
using Grpc.Net.Client;
using TidsOS.Contracts.Node;

namespace TidsOS.Agent;

public sealed class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    // A node going quiet (sleep, closed lid, flaky Wi-Fi) is the normal case
    // for this fleet, not an error — so reconnects back off instead of
    // hammering the controller, but never give up.
    private static readonly TimeSpan[] ReconnectBackoff =
    [
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60),
    ];

    private readonly SystemVitals _vitals = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Local/LAN controllers run plain HTTP/2 (h2c) — no cert required.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var controllerAddress = configuration["TIDSOS_CONTROLLER"] ?? "http://localhost:5270";
        var nodeId = NodeIdentity.GetOrCreateNodeId();
        logger.LogInformation("tidsOS agent starting. Node id {NodeId}, controller {Controller}", nodeId, controllerAddress);

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(controllerAddress, nodeId, stoppingToken);
                attempt = 0; // clean session end (e.g. controller cycled) — reset backoff
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                var delay = ReconnectBackoff[Math.Min(attempt, ReconnectBackoff.Length - 1)];
                attempt++;
                logger.LogWarning(ex, "Lost connection to controller at {Controller}; retrying in {Delay}", controllerAddress, delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task RunSessionAsync(string controllerAddress, string nodeId, CancellationToken stoppingToken)
    {
        using var channel = GrpcChannel.ForAddress(controllerAddress);
        var client = new NodeService.NodeServiceClient(channel);

        var registerResponse = await client.RegisterAsync(new RegisterRequest
        {
            NodeId = nodeId,
            Hostname = Environment.MachineName,
            OsDescription = RuntimeInformation.OSDescription,
            CpuCores = Environment.ProcessorCount,
            TotalMemoryMb = SystemVitals.AvailableMemoryMb(),
            AgentVersion = typeof(Worker).Assembly.GetName().Version?.ToString() ?? "0.1.0-dev",
        }, cancellationToken: stoppingToken);

        if (!registerResponse.Accepted)
        {
            logger.LogError("Controller rejected registration: {Message}", registerResponse.Message);
            return;
        }

        logger.LogInformation("Registered with controller: {Message}", registerResponse.Message);

        using var call = client.Heartbeat(cancellationToken: stoppingToken);

        var readAcksTask = Task.Run(async () =>
        {
            await foreach (var ack in call.ResponseStream.ReadAllAsync(stoppingToken))
            {
                logger.LogDebug("Heartbeat acknowledged at server time {ServerTime}", ack.ServerTimeUnixMs);
            }
        }, stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(1, registerResponse.HeartbeatIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await call.RequestStream.WriteAsync(new HeartbeatRequest
                {
                    NodeId = nodeId,
                    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    CpuUsagePercent = _vitals.SampleCpuUsagePercent(),
                    MemoryAvailableMb = SystemVitals.AvailableMemoryMb(),
                    Status = NodeStatus.Online,
                }, stoppingToken);
            }
        }
        finally
        {
            await call.RequestStream.CompleteAsync();
            await readAcksTask;
        }
    }
}
