using System.Text.Json;
using McpGuard.ToolRouter;
using ModelContextProtocol.Protocol;

namespace McpGuard.ToolRouter.Tests.Fakes;

public sealed class FakeMcpClientFactory : IMcpClientFactory
{
    public int CreateAsyncCallCount { get; private set; }
    private readonly Dictionary<string, CallToolResult> _resultsByTool = [];
    private Exception? _createAsyncException;
    private Exception? _callToolAsyncException;

    public FakeMcpClientFactory WithToolResult(string toolName, CallToolResult result)
    {
        _resultsByTool[toolName] = result;
        return this;
    }

    public FakeMcpClientFactory WithCreateAsyncException(Exception exception)
    {
        _createAsyncException = exception;
        return this;
    }

    public FakeMcpClientFactory WithCallToolAsyncException(Exception exception)
    {
        _callToolAsyncException = exception;
        return this;
    }

    public Task<IMcpDownstreamClient> CreateAsync(Uri downstreamUrl, CancellationToken ct)
    {
        CreateAsyncCallCount++;
        if (_createAsyncException is not null)
            return Task.FromException<IMcpDownstreamClient>(_createAsyncException);
        return Task.FromResult<IMcpDownstreamClient>(new FakeMcpDownstreamClient(_resultsByTool, _callToolAsyncException));
    }
}

public sealed class FakeMcpDownstreamClient : IMcpDownstreamClient
{
    private readonly Dictionary<string, CallToolResult> _resultsByTool;
    private readonly Exception? _callToolAsyncException;

    public int CallCount { get; private set; }

    public FakeMcpDownstreamClient(Dictionary<string, CallToolResult> resultsByTool, Exception? callToolAsyncException = null)
    {
        _resultsByTool = resultsByTool;
        _callToolAsyncException = callToolAsyncException;
    }

    public Task<CallToolResult> CallToolAsync(string toolName, JsonElement arguments, CancellationToken ct)
    {
        CallCount++;
        if (_callToolAsyncException is not null)
            return Task.FromException<CallToolResult>(_callToolAsyncException);
        if (!_resultsByTool.TryGetValue(toolName, out var result))
            throw new InvalidOperationException($"No result configured for tool '{toolName}'");

        return Task.FromResult(result);
    }

    public Task<ListToolsResult> ListToolsAsync(CancellationToken ct) =>
        throw new NotImplementedException("FakeMcpDownstreamClient.ListToolsAsync is not configured for M1 tests.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}