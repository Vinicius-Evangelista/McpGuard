namespace McpGuard.HealthChecks;

public sealed class DownstreamHealthCheckOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}