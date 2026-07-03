using McpGuard.ToolRouter;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;

namespace McpGuard.Gateway.Api;

public sealed class SdkMcpClientFactory : IMcpClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SdkMcpClientFactory> _logger;

    public SdkMcpClientFactory(ILoggerFactory loggerFactory, ILogger<SdkMcpClientFactory> logger)
    {
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct)
    {
        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = downstreamUrl
        };
        var transport = new HttpClientTransport(transportOptions, _loggerFactory);

        var clientOptions = new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "McpGuard", Version = "1.0.0" }
        };

        var client = await McpClient.CreateAsync(transport, clientOptions, _loggerFactory, ct);
        _logger.LogInformation("Created MCP client for {DownstreamUrl}", downstreamUrl);

        return new SdkMcpDownstreamClient(client);
    }
}