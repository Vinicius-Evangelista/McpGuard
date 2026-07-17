using System.Text.Json;
using McpGuard.HealthChecks;
using McpGuard.ServerRegistry;
using McpGuard.ToolRouter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpGuard.HealthChecks.Tests;

public sealed class DownstreamHealthCheckTests
{
    [Fact]
    public async Task Health_check_reports_healthy_when_all_enabled_servers_reachable()
    {
        var name = nameof(Health_check_reports_healthy_when_all_enabled_servers_reachable);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, "fast-one", new Uri("http://fast-one.local/mcp"), enabled: true);
        await SeedAsync(provider, "fast-two", new Uri("http://fast-two.local/mcp"), enabled: true);

        var check = new DownstreamHealthCheck(
            provider.GetRequiredService<IDbContextFactory<McpDbContext>>(),
            new FakeMcpClientFactory());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Health_check_reports_unhealthy_when_any_enabled_server_unreachable()
    {
        var name = nameof(Health_check_reports_unhealthy_when_any_enabled_server_unreachable);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, "fast-one", new Uri("http://fast-one.local/mcp"), enabled: true);
        await SeedAsync(provider, "throw-one", new Uri("http://throw-one.local/mcp"), enabled: true);

        var check = new DownstreamHealthCheck(
            provider.GetRequiredService<IDbContextFactory<McpDbContext>>(),
            new FakeMcpClientFactory());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Health_check_skips_disabled_servers()
    {
        var name = nameof(Health_check_skips_disabled_servers);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, "fast-one", new Uri("http://fast-one.local/mcp"), enabled: true);
        await SeedAsync(provider, "throw-one", new Uri("http://throw-one.local/mcp"), enabled: false);

        var check = new DownstreamHealthCheck(
            provider.GetRequiredService<IDbContextFactory<McpDbContext>>(),
            new FakeMcpClientFactory());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Health_check_respects_timeout_for_slow_server()
    {
        var name = nameof(Health_check_respects_timeout_for_slow_server);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, "slow-one", new Uri("http://slow-one.local/mcp"), enabled: true);

        var check = new DownstreamHealthCheck(
            provider.GetRequiredService<IDbContextFactory<McpDbContext>>(),
            new FakeMcpClientFactory(),
            new DownstreamHealthCheckOptions { Timeout = TimeSpan.FromMilliseconds(100) });

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("slow-one", result.Data.Keys);
    }

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<McpDbContext>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(
        ServiceProvider provider,
        string id,
        Uri downstreamUrl,
        bool enabled)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<McpDbContext>>();
        await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
        db.Servers.Add(new ServerEntity
        {
            Id = id,
            Name = id,
            DownstreamUrl = downstreamUrl,
            Enabled = enabled,
            DiscoveryState = "discovery-ok",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

internal sealed class FakeMcpClientFactory : IMcpClientFactory
{
    public Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct)
    {
        var host = downstreamUrl.Host;
        IMcpDownstreamClient client;
        if (host.StartsWith("throw", StringComparison.Ordinal))
        {
            client = new ThrowingMcpDownstreamClient();
        }
        else if (host.StartsWith("slow", StringComparison.Ordinal))
        {
            client = new SlowMcpDownstreamClient();
        }
        else
        {
            client = new FastMcpDownstreamClient();
        }
        return Task.FromResult(client);
    }
}

internal sealed class FastMcpDownstreamClient : IMcpDownstreamClient
{
    public Task<ListToolsResult> ListToolsAsync(CancellationToken ct) =>
        Task.FromResult(new ListToolsResult());

    public Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct) =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class SlowMcpDownstreamClient : IMcpDownstreamClient
{
    public async Task<ListToolsResult> ListToolsAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        return new ListToolsResult();
    }

    public Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct) =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class ThrowingMcpDownstreamClient : IMcpDownstreamClient
{
    public Task<ListToolsResult> ListToolsAsync(CancellationToken ct) =>
        throw new InvalidOperationException("downstream unavailable");

    public Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct) =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}