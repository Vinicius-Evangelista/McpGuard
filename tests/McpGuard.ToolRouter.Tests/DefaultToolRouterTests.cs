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
    private readonly FakeToolRegistry _registry;
    private readonly FakeAuditSink _audit;
    private readonly FakeMcpClientFactory _clientFactory;
    private readonly DefaultToolRouter _sut;

    public Default_tool_router()
    {
        _registry = new FakeToolRegistry(ObjectMother.AllToolRegistrations());
        _audit = new FakeAuditSink();
        _clientFactory = new FakeMcpClientFactory()
            .WithToolResult("echo", ObjectMother.EchoCallToolResult())
            .WithToolResult("add", ObjectMother.AddCallToolResult());
        _sut = new DefaultToolRouter(_registry, _audit, _clientFactory);
    }

    [Fact]
    public void List_visible_tools_hides_disallowed_and_invisible()
    {
        var visible = _sut.ListVisibleTools(CancellationToken.None);

        Assert.All(visible, t => Assert.True(t.Allowed && t.Visible));
        Assert.DoesNotContain(visible, t => t.Name == "dangerous");
        Assert.DoesNotContain(visible, t => t.Name == "secret");
    }

    [Fact]
    public async Task Route_call_on_allowed_tool_invokes_downstream_and_returns_result()
    {
        var result = await _sut.RouteCallAsync("echo", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.NotNull(result.Result);
        Assert.Null(result.BlockReason);
        Assert.Equal(1, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Route_call_on_allowed_tool_emits_allowed_audit_event()
    {
        await _sut.RouteCallAsync("echo", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

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
        var result = await _sut.RouteCallAsync("dangerous", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Null(result.Result);
        Assert.Equal(0, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Route_call_on_disallowed_tool_emits_blocked_audit_event()
    {
        await _sut.RouteCallAsync("dangerous", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

        var events = _audit.GetEvents();
        Assert.Single(events);
        Assert.Equal("tools.call.blocked", events[0].Outcome);
        Assert.Equal("dangerous", events[0].ToolName);
        Assert.Equal("session-1", events[0].SessionId);
    }

    [Fact]
    public async Task Route_call_on_invisible_tool_returns_blocked_and_never_invokes_downstream()
    {
        var result = await _sut.RouteCallAsync("secret", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Null(result.Result);
        Assert.Equal(0, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Route_call_on_unknown_tool_returns_blocked_and_never_invokes_downstream()
    {
        var result = await _sut.RouteCallAsync("nonexistent", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Null(result.Result);
        Assert.Equal(0, _clientFactory.CreateAsyncCallCount);
    }

    [Fact]
    public async Task Block_reason_names_the_tool()
    {
        var result = await _sut.RouteCallAsync("dangerous", ObjectMother.EchoCallArguments(), "session-1", CancellationToken.None);

        Assert.Equal("tool 'dangerous' is not approved for execution", result.BlockReason);
    }
}