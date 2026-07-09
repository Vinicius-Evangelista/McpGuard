using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace McpGuard.ToolRouter;

public interface IMcpDownstreamClient : IAsyncDisposable
{
    Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct);
}