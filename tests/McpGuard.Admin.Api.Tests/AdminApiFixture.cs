using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using McpGuard.CapabilityCatalog;
using McpGuard.ServerRegistry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace McpGuard.Admin.Api.Tests;

public sealed class AdminApiFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private string? _sqlitePath;
    public HttpClient HttpClient { get; private set; } = new();
    public FakeCapabilityDiscoverer Discoverer { get; } = new();

    public IDbContextFactory<McpDbContext> GetDbContextFactory() =>
        _app?.Services.GetRequiredService<IDbContextFactory<McpDbContext>>()
            ?? throw new InvalidOperationException("app not set");

    public async Task InitializeAsync()
    {
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"mcpguard-test-{Guid.NewGuid():N}.sqlite");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var connectionString = $"Data Source={_sqlitePath}";
        builder.Services.AddDbContextFactory<McpDbContext>(o => o.UseSqlite(connectionString));
        builder.Services.AddSingleton<ICapabilityDiscoverer>(Discoverer);
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        _app = app;

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<McpDbContext>>();
            var db = await factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }

        app.MapAdminEndpoints();
        app.MapHealthChecks("/health");

        await app.StartAsync();

        var baseAddress = new Uri(app.Urls.First());
        HttpClient = new HttpClient { BaseAddress = baseAddress };
    }

    public async Task DisposeAsync()
    {
        HttpClient.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
        if (_sqlitePath is not null)
        {
            foreach (var file in Directory.EnumerateFiles(
                Path.GetDirectoryName(_sqlitePath)!,
                Path.GetFileName(_sqlitePath) + "*"))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}

public sealed class FakeCapabilityDiscoverer : ICapabilityDiscoverer
{
    public IReadOnlyList<DiscoveredTool> Tools { get; set; } = new List<DiscoveredTool>();
    public Exception? ExceptionToThrow { get; set; }

    public Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(Uri downstreamUrl, CancellationToken ct)
    {
        if (ExceptionToThrow is not null)
        {
            return Task.FromException<IReadOnlyList<DiscoveredTool>>(ExceptionToThrow);
        }
        return Task.FromResult(Tools);
    }

    public Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(string serverId, CancellationToken ct)
    {
        if (ExceptionToThrow is not null)
        {
            return Task.FromException<IReadOnlyList<DiscoveredTool>>(ExceptionToThrow);
        }
        return Task.FromResult(Tools);
    }
}