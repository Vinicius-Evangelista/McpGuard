using System.Text.Json;

namespace McpGuard.ToolRouter;

public interface IToolRouter
{
    IReadOnlyList<ToolRegistry.ToolRegistration> ListVisibleTools(CancellationToken ct);
    Task<RouteResult> RouteCallAsync(string toolName, JsonElement arguments, string sessionId, CancellationToken ct);
}