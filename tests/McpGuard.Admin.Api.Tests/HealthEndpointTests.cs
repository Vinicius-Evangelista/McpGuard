using System.Net;
using System.Text.Json;
using McpGuard.Admin.Api.Tests;
using McpGuard.CapabilityCatalog;
using McpGuard.ServerRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpGuard.Admin.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<AdminApiFixture>
{
    private readonly AdminApiFixture _fixture;

    public HealthEndpointTests(AdminApiFixture fixture)
    {
        _fixture = fixture;
        _fixture.ClientFactory.ExceptionToThrow = null;
        _fixture.Discoverer.ExceptionToThrow = null;
        _fixture.Discoverer.Tools = new List<DiscoveredTool>();
    }

    [Fact]
    public async Task Health_endpoint_returns_200_when_all_servers_reachable()
    {
        await SeedServerAsync("healthy-one", new Uri("http://healthy-one.local/mcp"), enabled: true);

        var response = await _fixture.HttpClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ParseAsync(response);
        Assert.Equal("Healthy", payload.Status);
    }

    [Fact]
    public async Task Health_endpoint_returns_503_when_any_server_unreachable()
    {
        _fixture.ClientFactory.ExceptionToThrow = new InvalidOperationException("downstream unavailable");
        try
        {
            await SeedServerAsync("dead-one", new Uri("http://dead-one.local/mcp"), enabled: true);

            var response = await _fixture.HttpClient.GetAsync("/health");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var payload = await ParseAsync(response);
            Assert.Equal("Unhealthy", payload.Status);
        }
        finally
        {
            _fixture.ClientFactory.ExceptionToThrow = null;
        }
    }

    [Fact]
    public async Task Health_endpoint_response_includes_per_server_status()
    {
        await SeedServerAsync("reach-one", new Uri("http://reach-one.local/mcp"), enabled: true);
        await SeedServerAsync("reach-two", new Uri("http://reach-two.local/mcp"), enabled: true);

        var response = await _fixture.HttpClient.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var payload = await ParseAsync(response);
        Assert.NotEmpty(payload.Servers);
        Assert.Contains("reach-one", payload.Servers.Keys);
        Assert.Contains("reach-two", payload.Servers.Keys);
        Assert.Equal("healthy", payload.Servers["reach-one"]);
        Assert.Equal("healthy", payload.Servers["reach-two"]);
    }

    private async Task SeedServerAsync(string id, Uri downstreamUrl, bool enabled)
    {
        var factory = _fixture.GetDbContextFactory();
        await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
        db.Servers.Add(new ServerEntity
        {
            Id = id,
            Name = id,
            DownstreamUrl = downstreamUrl,
            Enabled = enabled,
            DiscoveryState = "discovery-ok",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<HealthPayload> ParseAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(stream);
        var status = doc.RootElement.GetProperty("status").GetString() ?? "";
        var servers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (doc.RootElement.TryGetProperty("servers", out var serversEl))
        {
            foreach (var property in serversEl.EnumerateObject())
            {
                servers[property.Name] = property.Value.GetString() ?? "";
            }
        }
        return new HealthPayload(status, servers);
    }

    private sealed record HealthPayload(string Status, Dictionary<string, string> Servers);
}