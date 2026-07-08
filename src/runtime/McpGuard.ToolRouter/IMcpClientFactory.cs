using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace McpGuard.ToolRouter;

public interface IMcpClientFactory
{
    Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct);
}

public interface IMcpDownstreamClient : IAsyncDisposable
{
    Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct);
}