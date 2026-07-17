using Microsoft.EntityFrameworkCore;
using McpGuard.ServerRegistry;
using McpGuard.ToolRouter;

namespace McpGuard.CapabilityCatalog;

public sealed class SdkCapabilityDiscoverer : ICapabilityDiscoverer
{
    private readonly IMcpClientFactory _clientFactory;
    private readonly IDbContextFactory<McpDbContext> _dbContextFactory;

    public SdkCapabilityDiscoverer(
        IMcpClientFactory clientFactory,
        IDbContextFactory<McpDbContext> dbContextFactory)
    {
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(Uri downstreamUrl, CancellationToken ct)
    {
        await using var client = await _clientFactory.CreateAsync(downstreamUrl, ct);
        var result = await client.ListToolsAsync(ct);

        var tools = result.Tools
            .Select(t => new DiscoveredTool(t.Name, t.Description ?? "", t.InputSchema))
            .ToList();

        return tools;
    }

    public async Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(string serverId, CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var server = await dbContext.Servers.FindAsync([serverId], ct);
        if (server is null)
            throw new InvalidOperationException($"Server '{serverId}' not found");

        return await DiscoverAsync(server.DownstreamUrl, ct);
    }
}