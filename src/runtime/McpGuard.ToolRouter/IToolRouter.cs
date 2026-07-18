using System.Text.Json;
using McpGuard.ToolRegistry;

namespace McpGuard.ToolRouter;

public interface IToolRouter
{
    Task<IReadOnlyList<ToolRegistration>> ListVisibleToolsAsync(CancellationToken ct);
    Task<RouteResult> RouteCallAsync(string toolName, JsonElement arguments, string sessionId, CancellationToken ct);
}