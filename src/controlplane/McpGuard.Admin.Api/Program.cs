using McpGuard.Admin.Api;
using McpGuard.CapabilityCatalog;
using McpGuard.HealthChecks;
using McpGuard.McpClient.Sdk;
using McpGuard.ServerRegistry;
using McpGuard.ToolRouter;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var sqlitePath = builder.Configuration.GetValue<string>("McpGuard:Store:SqlitePath") ?? "mcpguard.sqlite";
var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(sqlitePath));
if (!string.IsNullOrEmpty(dataDirectory))
{
    Directory.CreateDirectory(dataDirectory);
}
var connectionString = $"Data Source={sqlitePath}";

builder.Services.AddDbContextFactory<McpDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddSingleton<IMcpClientFactory, SdkMcpClientFactory>();
builder.Services.AddSingleton<ICapabilityDiscoverer, SdkCapabilityDiscoverer>();
builder.Services.AddHealthChecks().AddCheck<DownstreamHealthCheck>("downstream");

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

app.MapAdminEndpoints();
app.MapAdminHealthEndpoints();

app.Run();