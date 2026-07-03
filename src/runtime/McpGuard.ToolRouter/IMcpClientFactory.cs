using System.Text.Json;

namespace McpGuard.ToolRouter;

public interface IMcpClientFactory
{
    Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct);
}

public interface IMcpDownstreamClient : IAsyncDisposable
{
    Task<object> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct);
}