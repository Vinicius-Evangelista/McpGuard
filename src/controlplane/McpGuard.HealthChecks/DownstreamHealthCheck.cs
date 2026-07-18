using McpGuard.ServerRegistry;
using McpGuard.ToolRouter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace McpGuard.HealthChecks;

public sealed class DownstreamHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<McpDbContext> _factory;
    private readonly IMcpClientFactory _clientFactory;
    private readonly TimeSpan _timeout;

    public DownstreamHealthCheck(
        IDbContextFactory<McpDbContext> factory,
        IMcpClientFactory clientFactory,
        DownstreamHealthCheckOptions? options = null)
    {
        _factory = factory;
        _clientFactory = clientFactory;
        _timeout = options?.Timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var enabledServers = await db.Servers
            .Where(s => s.Enabled)
            .ToListAsync(cancellationToken);

        var perServer = new Dictionary<string, object>(StringComparer.Ordinal);
        var failed = false;

        foreach (var server in enabledServers)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_timeout);

            try
            {
                await using var client = await _clientFactory.CreateAsync(server.DownstreamUrl, linkedCts.Token);
                await client.ListToolsAsync(linkedCts.Token);
                perServer[server.Id] = "healthy";
            }
            catch (OperationCanceledException)
            {
                failed = true;
                perServer[server.Id] = "timed-out";
            }
            catch (Exception ex)
            {
                failed = true;
                perServer[server.Id] = $"failed: {ex.Message}";
            }
        }

        return failed
            ? HealthCheckResult.Unhealthy("One or more enabled downstream servers did not respond within the timeout.", data: perServer)
            : HealthCheckResult.Healthy("All enabled downstream servers responded within the timeout.", perServer);
    }
}