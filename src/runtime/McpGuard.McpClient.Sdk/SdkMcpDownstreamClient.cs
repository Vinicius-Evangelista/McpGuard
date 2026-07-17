using System.Text.Json;
using McpGuard.ToolRouter;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using McpClientInstance = ModelContextProtocol.Client.McpClient;

namespace McpGuard.McpClient.Sdk;

public sealed class SdkMcpDownstreamClient : IMcpDownstreamClient
{
    private readonly McpClientInstance _client;

    public SdkMcpDownstreamClient(McpClientInstance client)
    {
        _client = client;
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments)
            ?? new Dictionary<string, JsonElement>();

        var requestParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = args
        };

        return await _client.CallToolAsync(requestParams, ct);
    }

    public async Task<ListToolsResult> ListToolsAsync(CancellationToken ct)
    {
        return await _client.ListToolsAsync(new ListToolsRequestParams(), ct);
    }

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}