namespace TidsOS.Tests;

// https://learn.microsoft.com/aspnet/core/grpc/test-tools — TestServer
// reports responses as HTTP/1.1 by default, which Grpc.Net.Client's version
// check rejects; this forces the response version to match what was
// requested. Shared by every WebApplicationFactory-based gRPC test in this
// project.
internal sealed class ResponseVersionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        response.Version = request.Version;
        return response;
    }
}
