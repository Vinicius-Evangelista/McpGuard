using McpGuard.Audit;
using McpGuard.Gateway.Api;
using McpGuard.McpClient.Sdk;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ToolRegistryOptions>(
    builder.Configuration.GetSection("McpGuard"));

builder.Services.AddSingleton<IToolRegistry, ConfigToolRegistry>();
builder.Services.AddSingleton<IAuditSink, LoggerAuditSink>();
builder.Services.AddSingleton<IMcpClientFactory, SdkMcpClientFactory>();
builder.Services.AddSingleton<IToolRouter, DefaultToolRouter>();
builder.Services.AddSingleton<ISessionMigrationHandler>(sp => new AuditSessionHandler(sp.GetRequiredService<IAuditSink>()));
builder.Services.AddSingleton<IMcpGatewayHandler, McpGatewayHandler>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = builder.Configuration.GetValue<bool>("McpGuard:Stateless");
    })
    .WithListToolsHandler((ctx, ct) => ctx.Services!.GetRequiredService<IMcpGatewayHandler>().ListToolsAsync(ctx, ct))
    .WithCallToolHandler((ctx, ct) => ctx.Services!.GetRequiredService<IMcpGatewayHandler>().CallToolAsync(ctx, ct));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapMcp("/mcp");
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapMcp("/mcp");

app.Run();