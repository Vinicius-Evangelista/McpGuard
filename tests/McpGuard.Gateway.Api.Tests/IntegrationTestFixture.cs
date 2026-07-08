using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using McpGuard.Audit;
using McpGuard.Gateway.Api;
using McpGuard.Gateway.Api.Tests.Fakes;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using Xunit;

namespace McpGuard.Gateway.Api.Tests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private IContainer? _container;
    private WebApplication? _gatewayApp;
    private HttpClient? _httpClient;

    public CapturingAuditSink AuditSink { get; } = new();
    public HttpClient HttpClient => _httpClient ?? throw new InvalidOperationException("HttpClient not initialized");

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
        var downstreamBaseUrl = $"http://localhost:{downstreamPort}/mcp";

        var downstreamUrl = new Uri(downstreamBaseUrl);
        var testTools = new List<ToolRegistration>
        {
            new("echo", "Echoes the input message", downstreamUrl, Allowed: true, Visible: true),
            new("add", "Adds two integers", downstreamUrl, Allowed: true, Visible: true),
            new("secret", "An allowed but invisible tool", downstreamUrl, Allowed: true, Visible: false),
            new("dangerous", "A hidden, disallowed tool", downstreamUrl, Allowed: false, Visible: false),
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        Console.WriteLine("[FIXTURE] WebApplicationBuilder created, configuring...");
        builder.WebHost.UseUrls("http://127.0.0.1:5099");

        builder.Services.AddSingleton<IToolRegistry>(new TestToolRegistry(testTools));
        builder.Services.AddSingleton<IAuditSink>(AuditSink);
        builder.Services.AddSingleton<IMcpClientFactory, SdkMcpClientFactory>();
        builder.Services.AddSingleton<IToolRouter, DefaultToolRouter>();
        builder.Services.AddSingleton<IMcpGatewayHandler, McpGatewayHandler>();

        builder.Services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = builder.Configuration.GetValue<bool>("McpGuard:Stateless");
            })
            .WithListToolsHandler((ctx, ct) => ctx.Services!.GetRequiredService<IMcpGatewayHandler>().ListToolsAsync(ctx, ct))
            .WithCallToolHandler((ctx, ct) => ctx.Services!.GetRequiredService<IMcpGatewayHandler>().CallToolAsync(ctx, ct));

        _gatewayApp = builder.Build();
        _gatewayApp.MapMcp("/mcp");

        await _gatewayApp.StartAsync();

        var gatewayUrl = _gatewayApp.Urls.First();
        _httpClient = new HttpClient { BaseAddress = new Uri(gatewayUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<JsonElement> SendJsonRpcAsync(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = content };
        httpRequest.Headers.Add("Accept", "application/json, text/event-stream");

        using var response = await _httpClient!.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();

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
        _httpClient?.Dispose();
        if (_gatewayApp is not null)
        {
            await _gatewayApp.StopAsync();
            await _gatewayApp.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}