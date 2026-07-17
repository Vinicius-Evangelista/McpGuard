using System.Text.Json;
using McpGuard.Audit;
using McpGuard.ToolRegistry;

namespace McpGuard.ToolRouter;

public sealed class DefaultToolRouter : IToolRouter
{
    private readonly IAsyncToolRegistry _registry;
    private readonly IAuditSink _audit;
    private readonly IMcpClientFactory _clientFactory;

    public DefaultToolRouter(IAsyncToolRegistry registry, IAuditSink audit, IMcpClientFactory clientFactory)
    {
        _registry = registry;
        _audit = audit;
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<ToolRegistration>> ListVisibleToolsAsync(CancellationToken ct)
    {
        var tools = await _registry.GetAllAsync(ct);
        return tools
            .Where(t => t.Allowed && t.Visible)
            .ToList()
            .AsReadOnly();
    }

    public async Task<RouteResult> RouteCallAsync(string toolName, JsonElement arguments, string sessionId, CancellationToken ct)
    {
        var tool = await _registry.GetAsync(toolName, ct);

        if (tool is null || !tool.Allowed || !tool.Visible)
        {
            var blockReason = $"tool '{toolName}' is not approved for execution";
            await _audit.LogAsync(new AuditEvent(
                Timestamp: DateTimeOffset.UtcNow,
                SessionId: sessionId,
                Method: "tools/call",
                ToolName: toolName,
                Outcome: "tools.call.blocked",
                Reason: blockReason), ct);

            return new RouteResult(Allowed: false, Result: null, BlockReason: blockReason);
        }

        await _audit.LogAsync(new AuditEvent(
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            Method: "tools/call",
            ToolName: toolName,
            Outcome: "tools.call.allowed",
            Reason: null), ct);

        await using var client = await _clientFactory.CreateAsync(tool.DownstreamUrl, ct);
        var result = await client.CallToolAsync(toolName, arguments, ct);

        return new RouteResult(Allowed: true, Result: result, BlockReason: null);
    }
}