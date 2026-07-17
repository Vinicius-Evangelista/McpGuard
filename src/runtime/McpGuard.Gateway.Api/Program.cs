using McpGuard.Audit;
using McpGuard.Gateway.Api;
using McpGuard.McpClient.Sdk;
using McpGuard.ServerRegistry;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ToolRegistryOptions>(
    builder.Configuration.GetSection("McpGuard"));

var sqlitePath = builder.Configuration.GetValue<string>("McpGuard:Store:SqlitePath") ?? "mcpguard.db";
var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(sqlitePath));
if (!string.IsNullOrEmpty(dataDirectory))
{
    Directory.CreateDirectory(dataDirectory);
}
var connectionString = $"Data Source={sqlitePath}";

builder.Services.AddDbContextFactory<McpDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddSingleton<IToolRegistry, ConfigToolRegistry>();
builder.Services.AddSingleton<IAsyncToolRegistry, StoreToolRegistry>();
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

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<McpDbContext>>();
    var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapMcp("/mcp");

app.Run();