using System.Text.Json;
using McpGuard.Audit;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using McpGuard.ToolRouter.Tests.Fakes;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpGuard.ToolRouter.Tests;

public sealed class Default_tool_router
{
    private readonly FakeAsyncToolRegistry _registry;
    private readonly FakeAuditSink _audit;
    private readonly FakeMcpClientFactory _clientFactory;
    private readonly DefaultToolRouter _sut;

    public Default_tool_router()
    {
        _registry = new FakeAsyncToolRegistry(AllToolRegistrations());
        _audit = new FakeAuditSink();
        _clientFactory = new FakeMcpClientFactory()
            .WithToolResult("echo", EchoCallToolResult())
            .WithToolResult("add", AddCallToolResult());
        _sut = new DefaultToolRouter(_registry, _audit, _clientFactory);
    }

    [Fact]
    public async Task List_visible_tools_hides_disallowed_and_invisible()
    {
        var visible = await _sut.ListVisibleToolsAsync(CancellationToken.None);

        Assert.All(visible, t => Assert.True(t.Allowed && t.Visible));
        Assert.DoesNotContain(visible, t => t.Name == "dangerous");
        Assert.DoesNotContain(visible, t => t.Name == "secret");
    }

    [Fact]
    public async Task Route_call_on_allowed_tool_invokes_downstream_and_returns_result()
    {
        var result = await _sut.RouteCallAsync("echo", EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.NotNull(result.Result);
        Assert.Null(result.BlockReason);
        Assert.Equal(1, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Route_call_on_allowed_tool_emits_allowed_audit_event()
    {
        await _sut.RouteCallAsync("echo", EchoCallArguments(), "session-1", CancellationToken.None);

        var events = _audit.GetEvents();
        Assert.Single(events);
        Assert.Equal("tools.call.allowed", events[0].Outcome);
        Assert.Equal("echo", events[0].ToolName);
        Assert.Equal("session-1", events[0].SessionId);
        Assert.Equal("tools/call", events[0].Method);
    }

    [Fact]
    public async Task Route_call_on_disallowed_tool_returns_blocked_and_never_invokes_downstream()
    {
        var result = await _sut.RouteCallAsync("dangerous", EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Null(result.Result);
        Assert.Equal(0, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Route_call_on_disallowed_tool_emits_blocked_audit_event()
    {
        await _sut.RouteCallAsync("dangerous", EchoCallArguments(), "session-1", CancellationToken.None);

        var events = _audit.GetEvents();
        Assert.Single(events);
        Assert.Equal("tools.call.blocked", events[0].Outcome);
        Assert.Equal("dangerous", events[0].ToolName);
        Assert.Equal("session-1", events[0].SessionId);
    }

    [Fact]
    public async Task Route_call_on_invisible_tool_returns_blocked_and_never_invokes_downstream()
    {
        var result = await _sut.RouteCallAsync("secret", EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Null(result.Result);
        Assert.Equal(0, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Route_call_on_unknown_tool_returns_blocked_and_never_invokes_downstream()
    {
        var result = await _sut.RouteCallAsync("nonexistent", EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Null(result.Result);
        Assert.Equal(0, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Block_reason_names_the_tool()
    {
        var result = await _sut.RouteCallAsync("dangerous", EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.Equal("tool 'dangerous' is not approved for execution", result.BlockReason);
    }

    private static ToolRegistration ApprovedEchoTool() => new(
        Name: "echo",
        Description: "Echoes the input message",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: true,
        Visible: true);

    private static ToolRegistration ApprovedAddTool() => new(
        Name: "add",
        Description: "Adds two integers",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: true,
        Visible: true);

    private static ToolRegistration DisallowedDangerousTool() => new(
        Name: "dangerous",
        Description: "A hidden, disallowed tool",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: false,
        Visible: false);

    private static ToolRegistration InvisibleTool() => new(
        Name: "secret",
        Description: "An allowed but invisible tool",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: true,
        Visible: false);

    private static IReadOnlyList<ToolRegistration> AllToolRegistrations() =>
        [ApprovedEchoTool(), ApprovedAddTool(), DisallowedDangerousTool(), InvisibleTool()];

    private static JsonElement EchoCallArguments()
    {
        var json = """{"message":"hello"}""";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static CallToolResult EchoCallToolResult() => new()
    {
        Content = { new TextContentBlock { Text = "echo: hello" } }
    };

    private static CallToolResult AddCallToolResult() => new()
    {
        Content = { new TextContentBlock { Text = "42" } }
    };
}