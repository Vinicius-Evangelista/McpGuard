using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using McpGuard.Audit;
using McpGuard.Gateway.Api.Tests.Fakes;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpGuard.Gateway.Api.Tests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private IContainer? _container;
    private WebApplicationFactory<Program>? _factory;
    private McpClient? _mcpClient;
    private HttpClient? _httpClient;

    public CapturingAuditSink AuditSink { get; } = new();
    public McpClient McpClient => _mcpClient ?? throw new InvalidOperationException("McpClient not initialized");
    public string DownstreamBaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var dockerfilePath = Path.GetFullPath(
            Path.Combine("..", "..", "..", "..", "src", "runtime", "McpGuard.SampleTools.Server", "Dockerfile"));
        var contextPath = Path.GetFullPath(Path.Combine("..", "..", "..", ".."));

        var image = new ImageFromDockerfileBuilder()
            .WithDockerfile(dockerfilePath)
            .WithDockerfileDirectory(contextPath)
            .Build();

        _container = new ContainerBuilder(image)
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/").WithMethod(HttpMethod.Get)))
            .Build();

        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(8080);
        DownstreamBaseUrl = $"http://localhost:{port}/mcp";

        _factory = new GatewayWebApplicationFactory(AuditSink, DownstreamBaseUrl);
        _httpClient = _factory.CreateClient();
        var baseAddress = _httpClient.BaseAddress!;

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(baseAddress, "/mcp")
        };
        var transport = new HttpClientTransport(transportOptions, _factory.Services.GetRequiredService<ILoggerFactory>());

        var clientOptions = new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "McpGuardTestClient", Version = "1.0.0" }
        };

        _mcpClient = await McpClient.CreateAsync(transport, clientOptions,
            _factory.Services.GetRequiredService<ILoggerFactory>(), CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
        }
        _httpClient?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private sealed class GatewayWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly CapturingAuditSink _auditSink;
        private readonly string _downstreamBaseUrl;

        public GatewayWebApplicationFactory(CapturingAuditSink auditSink, string downstreamBaseUrl)
        {
            _auditSink = auditSink;
            _downstreamBaseUrl = downstreamBaseUrl;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var downstreamUrl = new Uri(_downstreamBaseUrl);

                var testTools = new List<ToolRegistration>
                {
                    new("echo", "Echoes the input message", downstreamUrl, Allowed: true, Visible: true),
                    new("add", "Adds two integers", downstreamUrl, Allowed: true, Visible: true),
                    new("dangerous", "A hidden, disallowed tool", downstreamUrl, Allowed: false, Visible: false),
                };

                ReplaceSingleton<IToolRegistry>(services, new TestToolRegistry(testTools));
                ReplaceSingleton<IAuditSink>(services, _auditSink);
            });
        }

        private static void ReplaceSingleton<TInterface>(IServiceCollection services, TInterface instance)
            where TInterface : class
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TInterface));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }
            services.AddSingleton(instance);
        }
    }
}