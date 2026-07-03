namespace McpGuard.Audit;

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string SessionId,
    string Method,
    string? ToolName,
    string Outcome,
    string? Reason);