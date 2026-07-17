using System.Text.Json;
using McpGuard.Audit;
using McpGuard.ToolRouter;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpGuard.Gateway.Api;

public interface IMcpGatewayHandler
{
    ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> context, CancellationToken ct);
    ValueTask<CallToolResult> CallToolAsync(RequestContext<CallToolRequestParams> context, CancellationToken ct);
}

public sealed class McpGatewayHandler : IMcpGatewayHandler
{
    private readonly IToolRouter _router;
    private readonly IAuditSink _audit;

    public McpGatewayHandler(IToolRouter router, IAuditSink audit)
    {
        _router = router;
        _audit = audit;
    }

    public async ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context, CancellationToken ct)
    {
        var sessionId = context.Server.SessionId ?? "";

        var visible = await _router.ListVisibleToolsAsync(ct);

        await _audit.LogAsync(new AuditEvent(
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            Method: "tools/list",
            ToolName: null,
            Outcome: "tools.listed",
            Reason: null), ct);

        var tools = new List<Tool>();
        foreach (var t in visible)
        {
            var tool = new Tool
            {
                Name = t.Name,
                Description = t.Description
            };
            if (t.InputSchema is { } schema && schema.ValueKind != JsonValueKind.Undefined)
            {
                tool.InputSchema = schema;
            }
            tools.Add(tool);
        }

        return new ListToolsResult { Tools = tools };
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context, CancellationToken ct)
    {
        var toolName = context.Params?.Name ?? "";
        var sessionId = context.Server.SessionId ?? "";

        var arguments = context.Params?.Arguments;
        var argumentsJson = arguments is not null
            ? JsonSerializer.SerializeToElement(arguments)
            : JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>());

        var routeResult = await _router.RouteCallAsync(toolName, argumentsJson, sessionId, ct);

        if (!routeResult.Allowed)
        {
            return new CallToolResult
            {
                Content = { new TextContentBlock { Text = routeResult.BlockReason ?? "tool call blocked" } },
                IsError = true
            };
        }

        return routeResult.Result ?? new CallToolResult
        {
            Content = { new TextContentBlock { Text = "unexpected result type from downstream" } },
            IsError = true
        };
    }
}