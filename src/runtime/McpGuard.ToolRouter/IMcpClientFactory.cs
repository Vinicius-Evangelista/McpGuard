using ModelContextProtocol.Protocol;

namespace McpGuard.ToolRouter;

public interface IMcpClientFactory
{
    Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct);
}