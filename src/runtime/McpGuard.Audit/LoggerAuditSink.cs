using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace McpGuard.Audit;

public sealed class LoggerAuditSink : IAuditSink
{
    private readonly ILogger<LoggerAuditSink> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LoggerAuditSink(ILogger<LoggerAuditSink> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(AuditEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, JsonOptions);
        _logger.LogInformation("{AuditLine}", json);
        return Task.CompletedTask;
    }
}