using McpGuard.ToolRouter;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using McpClientInstance = ModelContextProtocol.Client.McpClient;

namespace McpGuard.McpClient.Sdk;

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

        var client = await McpClientInstance.CreateAsync(transport, clientOptions, _loggerFactory, ct);
        _logger.LogInformation("Created MCP client for {DownstreamUrl}", downstreamUrl);

        return new SdkMcpDownstreamClient(client);
    }
}