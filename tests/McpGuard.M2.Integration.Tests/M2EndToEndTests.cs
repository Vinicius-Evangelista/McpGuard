using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using McpGuard.Admin.Api;
using Xunit;

namespace McpGuard.M2.Integration.Tests;

public sealed class M2_end_to_end : IClassFixture<M2IntegrationFixture>
{
    private readonly M2IntegrationFixture _fixture;

    public M2_end_to_end(M2IntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private static int _nextId = 1;
    private int NextId() => Interlocked.Increment(ref _nextId);

    private async Task InitializeSessionAsync(string clientName)
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
                clientInfo = new { name = clientName, version = "1.0.0" }
            }
        });
    }

    private Task<JsonElement> SendListAsync() =>
        _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/list",
            @params = new { }
        });

    private Task<JsonElement> SendCallAsync(string toolName, object arguments) =>
        _fixture.SendJsonRpcAsync(new
        {
            jsonrpc = "2.0",
            id = NextId(),
            method = "tools/call",
            @params = new { name = toolName, arguments }
        });

    private async Task<ServerDto> RegisterServerAsync(string name)
    {
        await _fixture.ResetStateAsync();
        var request = new RegisterServerRequest(name, _fixture.DownstreamUrl, true);
        var response = await _fixture.AdminHttpClient.PostAsJsonAsync("/servers", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ServerDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private async Task<CapabilityDto> GetCapabilityAsync(string serverId, string toolName)
    {
        var response = await _fixture.AdminHttpClient.GetAsync($"/servers/{serverId}/capabilities");
        response.EnsureSuccessStatusCode();
        var caps = await response.Content.ReadFromJsonAsync<List<CapabilityDto>>();
        Assert.NotNull(caps);
        var cap = caps!.First(c => c.ToolName == toolName);
        return cap;
    }

    [Fact]
    public async Task Register_server_via_admin_api_then_gateway_tools_list_reflects_discovered_tools()
    {
        var server = await RegisterServerAsync("m2-register-" + Guid.NewGuid().ToString("N")[..8]);

        await InitializeSessionAsync("m2-register-client");

        var response = await SendListAsync();

        var tools = response.GetProperty("result").GetProperty("tools");
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();

        Assert.Contains("echo", names);
        Assert.Contains("add", names);
        Assert.Equal("discovery-ok", server.DiscoveryState);
    }

    [Fact]
    public async Task Patch_capability_visible_false_drops_tool_from_gateway_tools_list_without_restart()
    {
        var server = await RegisterServerAsync("m2-visible-" + Guid.NewGuid().ToString("N")[..8]);
        var cap = await GetCapabilityAsync(server.Id, "echo");

        await InitializeSessionAsync("m2-visible-client");

        var before = await SendListAsync();
        var beforeNames = before.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.Contains("echo", beforeNames);

        var patchResponse = await _fixture.AdminHttpClient.PatchAsJsonAsync(
            $"/capabilities/{cap.Id}",
            new PatchCapabilityRequest(Allowed: null, Visible: false));
        patchResponse.EnsureSuccessStatusCode();

        var after = await SendListAsync();
        var afterNames = after.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.DoesNotContain("echo", afterNames);

        var restoreResponse = await _fixture.AdminHttpClient.PatchAsJsonAsync(
            $"/capabilities/{cap.Id}",
            new PatchCapabilityRequest(Allowed: null, Visible: true));
        restoreResponse.EnsureSuccessStatusCode();

        var restored = await SendListAsync();
        var restoredNames = restored.GetProperty("result").GetProperty("tools")
            .EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();
        Assert.Contains("echo", restoredNames);
    }

    [Fact]
    public async Task Patch_capability_allowed_false_blocks_gateway_tools_call()
    {
        var server = await RegisterServerAsync("m2-allowed-" + Guid.NewGuid().ToString("N")[..8]);
        var cap = await GetCapabilityAsync(server.Id, "echo");

        await InitializeSessionAsync("m2-allowed-client");

        var patchResponse = await _fixture.AdminHttpClient.PatchAsJsonAsync(
            $"/capabilities/{cap.Id}",
            new PatchCapabilityRequest(Allowed: false, Visible: null));
        patchResponse.EnsureSuccessStatusCode();

        _fixture.AuditSink.Clear();

        var response = await SendCallAsync("echo", new Dictionary<string, object?> { ["message"] = "blocked-test" });

        var result = response.GetProperty("result");
        Assert.True(result.TryGetProperty("isError", out var isErrorProp));
        Assert.True(isErrorProp.GetBoolean());

        var text = result.GetProperty("content").EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Contains("not approved for execution", text);
    }

    [Fact]
    public async Task Tools_call_on_unreachable_downstream_returns_isError_and_emits_downstream_unreachable_audit()
    {
        var server = await RegisterServerAsync("m2-unreachable-" + Guid.NewGuid().ToString("N")[..8]);

        await InitializeSessionAsync("m2-unreachable-client");

        var workingResponse = await SendCallAsync("echo", new Dictionary<string, object?> { ["message"] = "warmup" });
        var warmupResult = workingResponse.GetProperty("result");
        Assert.False(warmupResult.TryGetProperty("isError", out var warmupErr) && warmupErr.GetBoolean());

        var updateResponse = await _fixture.AdminHttpClient.PutAsJsonAsync(
            $"/servers/{server.Id}",
            new UpdateServerRequest(Name: null, DownstreamUrl: "http://127.0.0.1:1/mcp", Enabled: true));
        updateResponse.EnsureSuccessStatusCode();

        _fixture.AuditSink.Clear();

        var response = await SendCallAsync("echo", new Dictionary<string, object?> { ["message"] = "should-fail" });

        Console.WriteLine($"[DEBUG unreachable] response JSON: {response.GetRawText()}");
        var result = response.GetProperty("result");
        Assert.True(result.TryGetProperty("isError", out var isErrorProp));
        Assert.True(isErrorProp.GetBoolean());

        var text = result.GetProperty("content").EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString();
        Assert.Contains("downstream-unreachable:", text ?? "");
        Assert.Contains(server.Id, text ?? "");

        var auditEvents = await _fixture.AuditSink.WaitForEvents(1, TimeSpan.FromSeconds(10));
        var blockedEvent = auditEvents.FirstOrDefault(e =>
            e.Method == "tools/call" && e.Outcome == "tools.call.blocked");
        Assert.NotNull(blockedEvent);
        Assert.NotNull(blockedEvent.Reason);
        Assert.StartsWith("downstream-unreachable:", blockedEvent.Reason);
        Assert.Contains(server.Id, blockedEvent.Reason!);
    }

    [Fact]
    public async Task Health_endpoint_reports_unreachable_server_as_unhealthy()
    {
        await _fixture.ResetStateAsync();
        var registerRequest = new RegisterServerRequest(
            "m2-health-" + Guid.NewGuid().ToString("N")[..8],
            "http://127.0.0.1:1/mcp",
            true);
        var registerResponse = await _fixture.AdminHttpClient.PostAsJsonAsync("/servers", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var server = await registerResponse.Content.ReadFromJsonAsync<ServerDto>();
        Assert.NotNull(server);

        var response = await _fixture.AdminHttpClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        var servers = root.GetProperty("servers");
        Assert.True(servers.TryGetProperty(server!.Id, out var serverStatus));
        var statusText = serverStatus.GetString();
        Assert.False(string.IsNullOrEmpty(statusText));
        Assert.True(statusText == "timed-out" || statusText!.StartsWith("failed:"),
            $"expected 'timed-out' or 'failed: ...' but got '{statusText}'");
    }
}