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

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}