using System.Text.Json;
using McpGuard.ToolRouter;
using ModelContextProtocol.Protocol;

namespace McpGuard.ToolRouter.Tests.Fakes;

public sealed class FakeMcpClientFactory : IMcpClientFactory
{
    public int CreateAsyncCallCount { get; private set; }
    private readonly Dictionary<string, CallToolResult> _resultsByTool = [];

    public FakeMcpClientFactory WithToolResult(string toolName, CallToolResult result)
    {
        _resultsByTool[toolName] = result;
        return this;
    }

    public Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct)
    {
        CreateAsyncCallCount++;
        return Task.FromResult<IMcpDownstreamClient>(new FakeMcpDownstreamClient(_resultsByTool));
    }
}

public sealed class FakeMcpDownstreamClient : IMcpDownstreamClient
{
    private readonly Dictionary<string, CallToolResult> _resultsByTool;

    public int CallCount { get; private set; }

    public FakeMcpDownstreamClient(Dictionary<string, CallToolResult> resultsByTool) =>
        _resultsByTool = resultsByTool;

    public Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct)
    {
        CallCount++;
        if (!_resultsByTool.TryGetValue(toolName, out var result))
            throw new InvalidOperationException($"No result configured for tool '{toolName}'");

        return Task.FromResult(result);
    }

    public Task<ListToolsResult> ListToolsAsync(CancellationToken ct) =>
        throw new NotImplementedException("FakeMcpDownstreamClient.ListToolsAsync is not configured for M1 tests.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}