using System.Text.Json;
using McpGuard.Audit;
using McpGuard.ToolRegistry;

namespace McpGuard.ToolRouter;

public sealed class DefaultToolRouter : IToolRouter
{
    private readonly IToolRegistry _registry;
    private readonly IAuditSink _audit;
    private readonly IMcpClientFactory _clientFactory;

    public DefaultToolRouter(IToolRegistry registry, IAuditSink audit, IMcpClientFactory clientFactory)
    {
        _registry = registry;
        _audit = audit;
        _clientFactory = clientFactory;
    }

    public IReadOnlyList<ToolRegistration> ListVisibleTools(CancellationToken ct)
    {
        return _registry.GetAll(ct)
            .Where(t => t.Allowed && t.Visible)
            .ToList()
            .AsReadOnly();
    }

    public async Task<RouteResult> RouteCallAsync(string toolName, JsonElement arguments, string sessionId, CancellationToken ct)
    {
        var tool = _registry.Get(toolName, ct);

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