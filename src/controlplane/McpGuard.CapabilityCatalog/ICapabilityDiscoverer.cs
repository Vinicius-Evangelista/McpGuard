namespace McpGuard.CapabilityCatalog;

public interface ICapabilityDiscoverer
{
    Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(Uri downstreamUrl, CancellationToken ct);

    Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(string serverId, CancellationToken ct);
}