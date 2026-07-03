using System.Text.Json;
using McpGuard.Audit;
using McpGuard.Gateway.Api.Tests.Fakes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpGuard.Gateway.Api.Tests;

public sealed class End_to_end : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public End_to_end(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Initialize_negotiates_protocol_and_returns_session_id()
    {
        var client = _fixture.McpClient;
        Assert.NotNull(client.SessionId);
        Assert.NotEmpty(client.SessionId);
    }

    [Fact]
    public async Task Tools_list_returns_only_approved_tools()
    {
        var tools = await _fixture.McpClient.ListToolsAsync(options: null, CancellationToken.None);
        var names = tools.Select(t => t.Name).ToList();

        Assert.Contains("echo", names);
        Assert.Contains("add", names);
        Assert.DoesNotContain("dangerous", names);
    }

    [Fact]
    public async Task Tools_call_on_approved_echo_returns_downstream_result()
    {
        var args = new Dictionary<string, object?>
        {
            ["message"] = "hello from test"
        };

        var result = await _fixture.McpClient.CallToolAsync("echo", args, progress: null, options: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsError);

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.NotNull(text);
        Assert.Equal("hello from test", text!.Text);
    }

    [Fact]
    public async Task Tools_call_on_approved_add_returns_downstream_result()
    {
        var args = new Dictionary<string, object?>
        {
            ["a"] = 3,
            ["b"] = 7
        };

        var result = await _fixture.McpClient.CallToolAsync("add", args, progress: null, options: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsError);

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.NotNull(text);
        Assert.Equal("10", text!.Text);
    }

    [Fact]
    public async Task Tools_call_on_disallowed_tool_is_blocked_with_jsonrpc_error()
    {
        var args = new Dictionary<string, object?>();

        var result = await _fixture.McpClient.CallToolAsync("dangerous", args, progress: null, options: null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.NotNull(text);
        Assert.Contains("not approved for execution", text!.Text);
    }

    [Fact]
    public async Task Tools_call_on_invisible_tool_is_blocked_with_jsonrpc_error()
    {
        var args = new Dictionary<string, object?>();

        var result = await _fixture.McpClient.CallToolAsync("dangerous", args, progress: null, options: null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.NotNull(text);
        Assert.Contains("not approved for execution", text!.Text);
    }

    [Fact]
    public async Task Audit_emits_initialized_listed_allowed_and_blocked_events_in_order()
    {
        var events = await _fixture.AuditSink.WaitForEvents(3, TimeSpan.FromSeconds(10));
        var methods = events.Select(e => $"{e.Method}:{e.Outcome}").ToList();

        Assert.True(methods.Count >= 3,
            $"Expected at least 3 audit events, got {methods.Count}: {string.Join(", ", methods)}");

        Assert.Contains("tools/call:tools.call.allowed", methods);
        Assert.Contains("tools/call:tools.call.blocked", methods);
    }
}