using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using McpGuard.CapabilityCatalog;
using McpGuard.ServerRegistry;
using McpGuard.ToolRouter;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpGuard.CapabilityCatalog.Tests;

public sealed class SdkCapabilityDiscovererTests
{
    private static readonly Uri DownstreamUrl = new("http://downstream:8080");

    private static JsonElement ObjectSchema() =>
        JsonDocument.Parse("{\"type\":\"object\"}").RootElement;

    private static IDbContextFactory<McpDbContext> CreateDbContextFactory(params ServerEntity[] servers)
    {
        var options = new DbContextOptionsBuilder<McpDbContext>()
            .UseInMemoryDatabase($"discovery-{Guid.NewGuid():N}")
            .Options;
        var factory = new InMemoryDbContextFactory(options);
        using (var seed = factory.CreateDbContext())
        {
            seed.Servers.AddRange(servers);
            seed.SaveChanges();
        }
        return factory;
    }

    [Fact]
    public async Task Discover_async_via_downstream_returns_tools_with_schemas()
    {
        var tools = new List<Tool>
        {
            new() { Name = "echo", Description = "Echoes input", InputSchema = ObjectSchema() },
            new() { Name = "ping", Description = null, InputSchema = ObjectSchema() }
        };
        var factory = new FakeMcpClientFactory(tools);
        var discoverer = new SdkCapabilityDiscoverer(factory, CreateDbContextFactory());

        var result = await discoverer.DiscoverAsync(DownstreamUrl, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("echo", result[0].Name);
        Assert.Equal("Echoes input", result[0].Description);
        Assert.Equal("{\"type\":\"object\"}", result[0].InputSchema?.GetRawText());
        Assert.Equal("ping", result[1].Name);
        Assert.Equal("", result[1].Description);
    }

    [Fact]
    public async Task Discover_async_by_server_id_lookups_url_then_discovers()
    {
        var server = new ServerEntity
        {
            Id = "srv-1",
            Name = "demo",
            DownstreamUrl = DownstreamUrl,
            Enabled = true,
            DiscoveryState = "pending"
        };
        var tools = new List<Tool>
        {
            new() { Name = "lookup-tool", Description = "found via server id", InputSchema = ObjectSchema() }
        };
        var factory = new FakeMcpClientFactory(tools);
        var discoverer = new SdkCapabilityDiscoverer(factory, CreateDbContextFactory(server));

        var result = await discoverer.DiscoverAsync("srv-1", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("lookup-tool", result[0].Name);
        Assert.Equal("found via server id", result[0].Description);
    }

    [Fact]
    public async Task Discover_async_on_unreachable_downstream_throws()
    {
        var factory = new FakeMcpClientFactory(throwOnCreate: true);
        var discoverer = new SdkCapabilityDiscoverer(factory, CreateDbContextFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => discoverer.DiscoverAsync(DownstreamUrl, CancellationToken.None));
    }

    [Fact]
    public async Task Discover_async_on_empty_downstream_returns_empty_list()
    {
        var factory = new FakeMcpClientFactory(new List<Tool>());
        var discoverer = new SdkCapabilityDiscoverer(factory, CreateDbContextFactory());

        var result = await discoverer.DiscoverAsync(DownstreamUrl, CancellationToken.None);

        Assert.Empty(result);
    }
}

internal sealed class InMemoryDbContextFactory : IDbContextFactory<McpDbContext>
{
    private readonly DbContextOptions<McpDbContext> _options;

    public InMemoryDbContextFactory(DbContextOptions<McpDbContext> options) => _options = options;

    public McpDbContext CreateDbContext() => new(_options);

    public Task<McpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new McpDbContext(_options));
}

internal sealed class FakeMcpClientFactory : IMcpClientFactory
{
    private readonly List<Tool> _tools;
    private readonly bool _throwOnCreate;

    public FakeMcpClientFactory(List<Tool> tools) : this(tools, throwOnCreate: false) { }

    public FakeMcpClientFactory(bool throwOnCreate) : this(new List<Tool>(), throwOnCreate) { }

    private FakeMcpClientFactory(List<Tool> tools, bool throwOnCreate)
    {
        _tools = tools;
        _throwOnCreate = throwOnCreate;
    }

    public Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct)
    {
        if (_throwOnCreate)
            throw new InvalidOperationException($"downstream '{downstreamUrl}' unreachable");
        return Task.FromResult<IMcpDownstreamClient>(new FakeMcpDownstreamClient(_tools));
    }
}

internal sealed class FakeMcpDownstreamClient : IMcpDownstreamClient
{
    private readonly List<Tool> _tools;

    public FakeMcpDownstreamClient(List<Tool> tools) => _tools = tools;

    public Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<ListToolsResult> ListToolsAsync(CancellationToken ct) =>
        Task.FromResult(new ListToolsResult { Tools = _tools.ToList() });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}