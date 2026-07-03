namespace McpGuard.Audit;

public interface IAuditSink
{
    Task LogAsync(AuditEvent evt, CancellationToken ct);
}