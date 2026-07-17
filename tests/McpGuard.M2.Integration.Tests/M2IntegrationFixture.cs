using System.Net;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using McpGuard.Admin.Api;
using McpGuard.Audit;
using McpGuard.CapabilityCatalog;
using McpGuard.Gateway.Api;
using McpGuard.HealthChecks;
using McpGuard.McpClient.Sdk;
using McpGuard.ServerRegistry;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Xunit;

namespace McpGuard.M2.Integration.Tests;

public sealed class M2IntegrationFixture : IAsyncLifetime
{
    private IContainer? _container;
    private WebApplication? _adminApp;
    private WebApplication? _gatewayApp;
    private HttpClient? _adminHttpClient;
    private HttpClient? _gatewayHttpClient;
    private string? _sessionId;
    private string? _sqlitePath;

    public CapturingAuditSink AuditSink { get; } = new();
    public HttpClient AdminHttpClient => _adminHttpClient ?? throw new InvalidOperationException("AdminHttpClient not initialized");
    public HttpClient GatewayHttpClient => _gatewayHttpClient ?? throw new InvalidOperationException("GatewayHttpClient not initialized");
    public string DownstreamUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var repoRoot = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "..", "..", "..", "..", ".."));
        var dockerfileDir = Path.Combine(repoRoot, "src", "runtime", "McpGuard.SampleTools.Server");

        var futureImage = new ImageFromDockerfileBuilder()
            .WithDockerfile("Dockerfile")
            .WithDockerfileDirectory(dockerfileDir)
            .WithContextDirectory(repoRoot)
            .Build();

        await futureImage.CreateAsync();

        _container = new ContainerBuilder(futureImage)
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/").ForStatusCodeMatching(code => code == HttpStatusCode.OK || code == HttpStatusCode.NotFound)))
            .Build();

        await _container.StartAsync();

        var downstreamPort = _container.GetMappedPublicPort(8080);
        DownstreamUrl = $"http://localhost:{downstreamPort}/mcp";

        _sqlitePath = Path.Combine(Path.GetTempPath(), $"mcpguard-m2-{Guid.NewGuid():N}.sqlite");
        var connectionString = $"Data Source={_sqlitePath}";

        _adminApp = await BuildAdminAppAsync(connectionString);
        _gatewayApp = await BuildGatewayAppAsync(connectionString);

        _adminHttpClient = new HttpClient { BaseAddress = new Uri(_adminApp.Urls.First()), Timeout = TimeSpan.FromSeconds(30) };
        _gatewayHttpClient = new HttpClient { BaseAddress = new Uri(_gatewayApp.Urls.First()), Timeout = TimeSpan.FromSeconds(30) };
    }

    private static async Task<WebApplication> BuildAdminAppAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddDbContextFactory<McpDbContext>(o => o.UseSqlite(connectionString));
        builder.Services.AddSingleton<IMcpClientFactory, SdkMcpClientFactory>();
        builder.Services.AddSingleton<ICapabilityDiscoverer, SdkCapabilityDiscoverer>();
        builder.Services.AddHealthChecks().AddCheck<DownstreamHealthCheck>("downstream");

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<McpDbContext>>();
            var db = await factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }

        app.MapAdminEndpoints();
        app.MapAdminHealthEndpoints();

        await app.StartAsync();
        return app;
    }

    private async Task<WebApplication> BuildGatewayAppAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.Configure<ToolRegistryOptions>(_ => { });
        builder.Services.AddDbContextFactory<McpDbContext>(o => o.UseSqlite(connectionString));
        builder.Services.AddSingleton<IToolRegistry, ConfigToolRegistry>();
        builder.Services.AddSingleton<IAsyncToolRegistry, StoreToolRegistry>();
        builder.Services.AddSingleton<IAuditSink>(AuditSink);
        builder.Services.AddSingleton<IMcpClientFactory, SdkMcpClientFactory>();
        builder.Services.AddSingleton<IToolRouter, DefaultToolRouter>();
        builder.Services.AddSingleton<ISessionMigrationHandler>(sp => new AuditSessionHandler(sp.GetRequiredService<IAuditSink>()));
        builder.Services.AddSingleton<IMcpGatewayHandler, McpGatewayHandler>();

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = false;
            })
            .WithListToolsHandler((ctx, ct) => ctx.Services!.GetRequiredService<IMcpGatewayHandler>().ListToolsAsync(ctx, ct))
            .WithCallToolHandler((ctx, ct) => ctx.Services!.GetRequiredService<IMcpGatewayHandler>().CallToolAsync(ctx, ct));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<McpDbContext>>();
            var db = await factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }

        app.MapMcp("/mcp");

        await app.StartAsync();
        return app;
    }

    public async Task ResetStateAsync()
    {
        await using var scope = _adminApp!.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<McpDbContext>>();
        var db = await factory.CreateDbContextAsync();
        db.Capabilities.RemoveRange(db.Capabilities);
        db.Servers.RemoveRange(db.Servers);
        await db.SaveChangesAsync();
        _sessionId = null;
        AuditSink.Clear();
    }

    public async Task<JsonElement> SendJsonRpcAsync(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = content };
        httpRequest.Headers.Add("Accept", "application/json, text/event-stream");
        if (_sessionId is not null)
        {
            httpRequest.Headers.Add("Mcp-Session-Id", _sessionId);
        }

        using var response = await _gatewayHttpClient!.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();

        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
        {
            _sessionId = sessionIds.First();
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        if (contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in responseBody.Split('\n'))
            {
                if (line.StartsWith("data: ", StringComparison.OrdinalIgnoreCase))
                {
                    var dataJson = line["data: ".Length..];
                    using var doc = JsonDocument.Parse(dataJson);
                    return doc.RootElement.Clone();
                }
            }

            throw new InvalidOperationException($"No data line found in SSE response: {responseBody}");
        }

        using var jsonDoc = JsonDocument.Parse(responseBody);
        return jsonDoc.RootElement.Clone();
    }

    public async Task DisposeAsync()
    {
        _adminHttpClient?.Dispose();
        _gatewayHttpClient?.Dispose();
        if (_gatewayApp is not null)
        {
            await _gatewayApp.StopAsync();
            await _gatewayApp.DisposeAsync();
        }
        if (_adminApp is not null)
        {
            await _adminApp.StopAsync();
            await _adminApp.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        if (_sqlitePath is not null)
        {
            foreach (var file in Directory.EnumerateFiles(
                Path.GetDirectoryName(_sqlitePath)!,
                Path.GetFileName(_sqlitePath) + "*"))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}