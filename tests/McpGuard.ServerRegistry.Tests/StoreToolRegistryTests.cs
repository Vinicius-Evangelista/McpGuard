using System.Text.Json;
using McpGuard.ServerRegistry;
using McpGuard.ToolRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGuard.ServerRegistry.Tests;

public sealed class StoreToolRegistryTests
{
    [Fact]
    public async Task Store_tool_registry_returns_capabilities_from_enabled_servers()
    {
        var name = nameof(Store_tool_registry_returns_capabilities_from_enabled_servers);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, server => { AddEcho(server); AddAdd(server); });

        var registry = new StoreToolRegistry(provider.GetRequiredService<IDbContextFactory<McpDbContext>>());
        var tools = await registry.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "echo");
        Assert.Contains(tools, t => t.Name == "add");
        var echo = tools.First(t => t.Name == "echo");
        Assert.Equal("Echoes the input message", echo.Description);
        Assert.Equal(new Uri("http://localhost:5010/mcp"), echo.DownstreamUrl);
        Assert.True(echo.Allowed);
        Assert.True(echo.Visible);
        Assert.NotNull(echo.ServerId);
        Assert.NotNull(echo.CapabilityId);
    }

    [Fact]
    public async Task Store_tool_registry_returns_null_for_unknown_tool()
    {
        var name = nameof(Store_tool_registry_returns_null_for_unknown_tool);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, server => AddEcho(server));

        var registry = new StoreToolRegistry(provider.GetRequiredService<IDbContextFactory<McpDbContext>>());
        var result = await registry.GetAsync("does_not_exist", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Store_tool_registry_excludes_disabled_servers()
    {
        var name = nameof(Store_tool_registry_excludes_disabled_servers);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, server => AddEcho(server), enabled: false);
        await SeedAsync(provider, server => AddAdd(server), enabled: true);

        var registry = new StoreToolRegistry(provider.GetRequiredService<IDbContextFactory<McpDbContext>>());
        var tools = await registry.GetAllAsync(CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal("add", tool.Name);
    }

    [Fact]
    public async Task Store_tool_registry_reads_live_no_cache_between_calls()
    {
        var name = nameof(Store_tool_registry_reads_live_no_cache_between_calls);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, server => AddEcho(server));

        var factory = provider.GetRequiredService<IDbContextFactory<McpDbContext>>();
        var registry = new StoreToolRegistry(factory);

        var first = await registry.GetAllAsync(CancellationToken.None);
        Assert.Single(first);

        await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
        var server = await db.Servers.FirstAsync();
        server.Capabilities.Add(new CapabilityEntity
        {
            ToolName = "add",
            Description = "Adds two integers",
            InputSchemaJson = null,
            Allowed = true,
            Visible = true,
            SyncedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var second = await registry.GetAllAsync(CancellationToken.None);
        Assert.Equal(2, second.Count);
    }

    [Fact]
    public async Task Store_tool_registry_maps_input_schema_from_json()
    {
        var name = nameof(Store_tool_registry_maps_input_schema_from_json);
        using var provider = BuildProvider(name);
        await SeedAsync(provider, server =>
        {
            server.Capabilities.Add(new CapabilityEntity
            {
                ToolName = "echo",
                Description = "Echoes the input message",
                InputSchemaJson = """
                                  {"type":"object","properties":{"message":{"type":"string"}}}
                                  """,
                Allowed = true,
                Visible = true,
                SyncedAt = DateTimeOffset.UtcNow
            });
        });

        var registry = new StoreToolRegistry(provider.GetRequiredService<IDbContextFactory<McpDbContext>>());
        var echo = await registry.GetAsync("echo", CancellationToken.None);

        Assert.NotNull(echo);
        Assert.NotNull(echo.InputSchema);
        Assert.Equal("object", echo.InputSchema!.Value.GetProperty("type").GetString());
    }

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<McpDbContext>(o => o.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(
        ServiceProvider provider,
        Action<ServerEntity> configureServer,
        bool enabled = true)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<McpDbContext>>();
        await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
        var server = new ServerEntity
        {
            Name = Guid.NewGuid().ToString("N"),
            DownstreamUrl = new Uri("http://localhost:5010/mcp"),
            Enabled = enabled,
            DiscoveryState = "discovery-ok",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        configureServer(server);
        db.Servers.Add(server);
        await db.SaveChangesAsync();
    }

    private static void AddEcho(ServerEntity server)
    {
        server.Capabilities.Add(new CapabilityEntity
        {
            ToolName = "echo",
            Description = "Echoes the input message",
            InputSchemaJson = null,
            Allowed = true,
            Visible = true,
            SyncedAt = DateTimeOffset.UtcNow
        });
    }

    private static void AddAdd(ServerEntity server)
    {
        server.Capabilities.Add(new CapabilityEntity
        {
            ToolName = "add",
            Description = "Adds two integers",
            InputSchemaJson = null,
            Allowed = true,
            Visible = true,
            SyncedAt = DateTimeOffset.UtcNow
        });
    }
}