using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using McpGuard.Admin.Api.Tests;
using McpGuard.CapabilityCatalog;
using McpGuard.ServerRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGuard.Admin.Api.Tests;

public sealed class AdminEndpointsTests : IClassFixture<AdminApiFixture>
{
    private readonly AdminApiFixture _fixture;

    public AdminEndpointsTests(AdminApiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Discoverer.ExceptionToThrow = null;
        _fixture.Discoverer.Tools = new List<DiscoveredTool>
        {
            new("echo", "Echoes the input message", null),
            new("add", "Adds two integers", null)
        };
    }

    [Fact]
    public async Task Register_server_persists_and_triggers_discovery()
    {
        var request = new RegisterServerRequest("server-1", "http://localhost:5010/mcp", true);
        var response = await _fixture.HttpClient.PostAsJsonAsync("/servers", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<ServerDto>();
        Assert.NotNull(dto);
        Assert.Equal("server-1", dto.Name);
        Assert.Equal("discovery-ok", dto.DiscoveryState);
        Assert.True(dto.Enabled);

        var caps = await GetCapabilitiesAsync(dto.Id);
        Assert.Equal(2, caps.Count);
        Assert.Contains(caps, c => c.ToolName == "echo");
        Assert.Contains(caps, c => c.ToolName == "add");
    }

    [Fact]
    public async Task Register_server_with_invalid_url_returns_400()
    {
        var request = new RegisterServerRequest("server-bad", "not-a-url", true);
        var response = await _fixture.HttpClient.PostAsJsonAsync("/servers", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_server_when_discovery_fails_persists_with_discovery_failed_state()
    {
        _fixture.Discoverer.ExceptionToThrow = new InvalidOperationException("boom");
        try
        {
            var request = new RegisterServerRequest("server-fail", "http://localhost:5010/mcp", true);
            var response = await _fixture.HttpClient.PostAsJsonAsync("/servers", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Discovery-Warning"));

            var dto = await response.Content.ReadFromJsonAsync<ServerDto>();
            Assert.NotNull(dto);
            Assert.Equal("discovery-failed", dto.DiscoveryState);
            var caps = await GetCapabilitiesAsync(dto.Id);
            Assert.Empty(caps);
        }
        finally
        {
            _fixture.Discoverer.ExceptionToThrow = null;
        }
    }

    [Fact]
    public async Task Get_servers_returns_all()
    {
        await PostServerAsync("alpha-all", "http://localhost:7001/mcp");
        await PostServerAsync("beta-all", "http://localhost:7002/mcp");

        var response = await _fixture.HttpClient.GetAsync("/servers");
        response.EnsureSuccessStatusCode();

        var servers = await response.Content.ReadFromJsonAsync<List<ServerDto>>();
        Assert.NotNull(servers);
        Assert.Contains(servers, s => s.Name == "alpha-all");
        Assert.Contains(servers, s => s.Name == "beta-all");
    }

    [Fact]
    public async Task Get_server_by_id_returns_404_for_unknown()
    {
        var response = await _fixture.HttpClient.GetAsync("/servers/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_server_updates_mutable_fields()
    {
        var created = await PostServerAsync("put-target", "http://localhost:7010/mcp");

        var update = new UpdateServerRequest("renamed", "http://localhost:7011/mcp", false);
        var response = await _fixture.HttpClient.PutAsJsonAsync($"/servers/{created.Id}", update);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ServerDto>();
        Assert.NotNull(dto);
        Assert.Equal("renamed", dto.Name);
        Assert.Equal("http://localhost:7011/mcp", dto.DownstreamUrl);
        Assert.False(dto.Enabled);
    }

    [Fact]
    public async Task Delete_server_returns_204_or_404()
    {
        var created = await PostServerAsync("delete-target", "http://localhost:7020/mcp");

        var delete1 = await _fixture.HttpClient.DeleteAsync($"/servers/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete1.StatusCode);

        var delete2 = await _fixture.HttpClient.DeleteAsync($"/servers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete2.StatusCode);
    }

    [Fact]
    public async Task Resync_server_replaces_capabilities_and_updates_synced_at()
    {
        var created = await PostServerAsync("resync-target", "http://localhost:7030/mcp");
        var before = await GetCapabilitiesAsync(created.Id);
        Assert.Equal(2, before.Count);

        _fixture.Discoverer.Tools = new List<DiscoveredTool>
        {
            new("only-tool", "Single replacement tool", null)
        };
        try
        {
            var response = await _fixture.HttpClient.PostAsJsonAsync($"/servers/{created.Id}/resync", new { });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ResyncResultDto>();
            Assert.NotNull(result);
            Assert.Equal(created.Id, result!.ServerId);
            Assert.Equal(1, result.CapabilitiesCount);

            var after = await GetCapabilitiesAsync(created.Id);
            Assert.Single(after);
            Assert.Equal("only-tool", after[0].ToolName);
            Assert.True(after[0].SyncedAt >= before[0].SyncedAt);
        }
        finally
        {
            _fixture.Discoverer.Tools = new List<DiscoveredTool>
            {
                new("echo", "Echoes the input message", null),
                new("add", "Adds two integers", null)
            };
        }
    }

    [Fact]
    public async Task Resync_unknown_server_returns_404()
    {
        var response = await _fixture.HttpClient.PostAsJsonAsync("/servers/nonexistent/resync", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_capability_visibility_reflects_in_next_query()
    {
        var created = await PostServerAsync("patch-vis", "http://localhost:7040/mcp");
        var capsResponse = await _fixture.HttpClient.GetAsync("/capabilities");
        capsResponse.EnsureSuccessStatusCode();
        var caps = await capsResponse.Content.ReadFromJsonAsync<List<CapabilityDto>>();
        Assert.NotNull(caps);
        var cap = caps!.First(c => c.ServerId == created.Id);

        var patch = new PatchCapabilityRequest(null, false);
        var patchResponse = await _fixture.HttpClient.PatchAsJsonAsync($"/capabilities/{cap.Id}", patch);
        patchResponse.EnsureSuccessStatusCode();
        var patched = await patchResponse.Content.ReadFromJsonAsync<CapabilityDto>();
        Assert.NotNull(patched);
        Assert.False(patched!.Visible);

        var getResponse = await _fixture.HttpClient.GetAsync($"/capabilities/{cap.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<CapabilityDto>();
        Assert.NotNull(fetched);
        Assert.False(fetched!.Visible);
    }

    [Fact]
    public async Task Patch_capability_allowed_reflects_in_next_query()
    {
        var created = await PostServerAsync("patch-allow", "http://localhost:7050/mcp");
        var capsResponse = await _fixture.HttpClient.GetAsync("/capabilities");
        capsResponse.EnsureSuccessStatusCode();
        var caps = await capsResponse.Content.ReadFromJsonAsync<List<CapabilityDto>>();
        Assert.NotNull(caps);
        var cap = caps!.First(c => c.ServerId == created.Id);

        var patch = new PatchCapabilityRequest(false, null);
        var patchResponse = await _fixture.HttpClient.PatchAsJsonAsync($"/capabilities/{cap.Id}", patch);
        patchResponse.EnsureSuccessStatusCode();
        var patched = await patchResponse.Content.ReadFromJsonAsync<CapabilityDto>();
        Assert.NotNull(patched);
        Assert.False(patched!.Allowed);

        var getResponse = await _fixture.HttpClient.GetAsync($"/capabilities/{cap.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<CapabilityDto>();
        Assert.NotNull(fetched);
        Assert.False(fetched!.Allowed);
    }

    [Fact]
    public async Task Patch_unknown_capability_returns_404()
    {
        var patch = new PatchCapabilityRequest(true, null);
        var response = await _fixture.HttpClient.PatchAsJsonAsync("/capabilities/nonexistent", patch);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_capabilities_returns_all()
    {
        var a = await PostServerAsync("caps-all-a", "http://localhost:7060/mcp");
        var b = await PostServerAsync("caps-all-b", "http://localhost:7061/mcp");

        var response = await _fixture.HttpClient.GetAsync("/capabilities");
        response.EnsureSuccessStatusCode();
        var caps = await response.Content.ReadFromJsonAsync<List<CapabilityDto>>();
        Assert.NotNull(caps);
        Assert.Contains(caps!, c => c.ServerId == a.Id);
        Assert.Contains(caps!, c => c.ServerId == b.Id);
        Assert.True(caps!.Count >= 4);
    }

    [Fact]
    public async Task Get_capability_by_id_returns_404_for_unknown()
    {
        var response = await _fixture.HttpClient.GetAsync("/capabilities/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_server_capabilities_returns_only_that_server()
    {
        var a = await PostServerAsync("caps-scope-a", "http://localhost:7070/mcp");
        var b = await PostServerAsync("caps-scope-b", "http://localhost:7071/mcp");

        var response = await _fixture.HttpClient.GetAsync($"/servers/{a.Id}/capabilities");
        response.EnsureSuccessStatusCode();
        var caps = await response.Content.ReadFromJsonAsync<List<CapabilityDto>>();
        Assert.NotNull(caps);
        Assert.NotEmpty(caps);
        Assert.All(caps!, c => Assert.Equal(a.Id, c.ServerId));
        Assert.DoesNotContain(caps!, c => c.ServerId == b.Id);
    }

    private async Task<ServerDto> PostServerAsync(string name, string url)
    {
        var request = new RegisterServerRequest(name, url, true);
        var response = await _fixture.HttpClient.PostAsJsonAsync("/servers", request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ServerDto>();
        Assert.NotNull(dto);
        return dto;
    }

    private async Task<List<CapabilityEntity>> GetCapabilitiesAsync(string serverId)
    {
        var factory = _fixture.GetDbContextFactory();
        await using var db = await factory.CreateDbContextAsync();
        return await db.Capabilities.Where(c => c.ServerId == serverId).ToListAsync();
    }
}