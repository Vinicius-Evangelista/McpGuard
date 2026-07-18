using System.Text.Json;
using Xunit;

namespace McpGuard.Gateway.Api.Tests;

public sealed class End_to_end : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public End_to_end(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static int _nextId = 1;
    private int NextId() => Interlocked.Increment(ref _nextId);

    [Fact]
    public async Task Initialize_negotiates_protocol_and_returns_server_info()
    {
        var response = await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client", version = "1.0.0" }
            }
        });

        var result = response.GetProperty("result");
        Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
        Assert.NotNull(result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Tools_list_returns_only_approved_tools()
    {
        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client-list", version = "1.0.0" }
            }
        });

        var response = await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/list",
            @params = new { }
        });

        var tools = response.GetProperty("result").GetProperty("tools");
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();

        Assert.Contains("echo", names);
        Assert.Contains("add", names);
        Assert.DoesNotContain("dangerous", names);
    }

    [Fact]
    public async Task Tools_call_on_approved_echo_returns_downstream_result()
    {
        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client-echo", version = "1.0.0" }
            }
        });

        var response = await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new
            {
                name = "echo",
                arguments = new Dictionary<string, object?> { ["message"] = "hello from test" }
            }
        });

        var result = response.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var isErrorProp) && isErrorProp.GetBoolean());

        var content = result.GetProperty("content");
        var text = content.EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Equal("hello from test", text);
    }

    [Fact]
    public async Task Tools_call_on_approved_add_returns_downstream_result()
    {
        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client-add", version = "1.0.0" }
            }
        });

        var response = await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new
            {
                name = "add",
                arguments = new Dictionary<string, object?> { ["a"] = 3, ["b"] = 7 }
            }
        });

        var result = response.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var isErrorProp) && isErrorProp.GetBoolean());

        var content = result.GetProperty("content");
        var text = content.EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Equal("10", text);
    }

    [Fact]
    public async Task Tools_call_on_disallowed_tool_is_blocked_with_jsonrpc_error()
    {
        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client-blocked", version = "1.0.0" }
            }
        });

        var response = await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new
            {
                name = "dangerous",
                arguments = new Dictionary<string, object?>()
            }
        });

        var result = response.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());

        var content = result.GetProperty("content");
        var text = content.EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Contains("not approved for execution", text);
    }

    [Fact]
    public async Task Tools_call_on_invisible_tool_is_blocked_with_jsonrpc_error()
    {
        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client-invisible", version = "1.0.0" }
            }
        });

        var response = await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new
            {
                name = "secret",
                arguments = new Dictionary<string, object?>()
            }
        });

        var result = response.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());

        var content = result.GetProperty("content");
        var text = content.EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Contains("not approved for execution", text);
    }

    [Fact]
    public async Task Audit_emits_initialized_listed_allowed_and_blocked_events_in_order()
    {
        _fixture.AuditSink.Clear();

        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client-audit", version = "1.0.0" }
            }
        });

        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/list",
            @params = new { }
        });

        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new
            {
                name = "echo",
                arguments = new Dictionary<string, object?> { ["message"] = "audit test" }
            }
        });

        await _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new
            {
                name = "dangerous",
                arguments = new Dictionary<string, object?>()
            }
        });

        var events = await _fixture.AuditSink.WaitForEvents(4, TimeSpan.FromSeconds(10));
        var methods = events.Select(e => $"{e.Method}:{e.Outcome}").ToList();

        Assert.Equal(4, methods.Count);
        Assert.Equal("initialize:initialized", methods[0]);
        Assert.Equal("tools/list:tools.listed", methods[1]);
        Assert.Equal("tools/call:tools.call.allowed", methods[2]);
        Assert.Equal("tools/call:tools.call.blocked", methods[3]);
    }
}