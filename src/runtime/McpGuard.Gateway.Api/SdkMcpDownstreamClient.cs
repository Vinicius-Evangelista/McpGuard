using System.Text.Json;
using McpGuard.ToolRouter;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpGuard.Gateway.Api;

public sealed class SdkMcpDownstreamClient : IMcpDownstreamClient
{
    private readonly McpClient _client;

    public SdkMcpDownstreamClient(McpClient client)
    {
        _client = client;
    }

    public async Task<object> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments)
            ?? new Dictionary<string, JsonElement>();

        var requestParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = args
        };

        var result = await _client.CallToolAsync(requestParams, ct);
        return result;
    }

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}