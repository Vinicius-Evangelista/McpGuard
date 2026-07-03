using System.Text.Json;
using McpGuard.Audit;
using McpGuard.Gateway.Api;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ToolRegistryOptions>(
    builder.Configuration.GetSection("McpGuard:Tools"));

builder.Services.AddSingleton<IToolRegistry, ConfigToolRegistry>();
builder.Services.AddSingleton<IAuditSink, LoggerAuditSink>();
builder.Services.AddSingleton<IMcpClientFactory, SdkMcpClientFactory>();
builder.Services.AddSingleton<IToolRouter, DefaultToolRouter>();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithListToolsHandler(ListToolsHandler)
    .WithCallToolHandler(CallToolHandler);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapMcp("/mcp");

app.Run();

ValueTask<ListToolsResult> ListToolsHandler(RequestContext<ListToolsRequestParams> context, CancellationToken ct)
{
    var router = context.Services!.GetRequiredService<IToolRouter>();
    var visible = router.ListVisibleTools(ct);

    var tools = visible.Select(t => new Tool
    {
        Name = t.Name,
        Description = t.Description
    }).ToList();

    return new ValueTask<ListToolsResult>(new ListToolsResult { Tools = tools });
}

async ValueTask<CallToolResult> CallToolHandler(RequestContext<CallToolRequestParams> context, CancellationToken ct)
{
    var router = context.Services!.GetRequiredService<IToolRouter>();

    var toolName = context.Params?.Name ?? "";
    var sessionId = context.Server.SessionId ?? "";

    var arguments = context.Params?.Arguments;
    var argumentsJson = arguments is not null
        ? JsonSerializer.SerializeToElement(arguments)
        : JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>());

    var routeResult = await router.RouteCallAsync(toolName, argumentsJson, sessionId, ct);

    if (!routeResult.Allowed)
    {
        return new CallToolResult
        {
            Content = { new TextContentBlock { Text = routeResult.BlockReason ?? "tool call blocked" } },
            IsError = true
        };
    }

    if (routeResult.Result is CallToolResult downstreamResult)
    {
        return downstreamResult;
    }

    return new CallToolResult
    {
        Content = { new TextContentBlock { Text = "unexpected result type from downstream" } },
        IsError = true
    };
}