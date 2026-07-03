using McpGuard.Audit;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace McpGuard.Gateway.Api;

public sealed class AuditSessionHandler : ISessionMigrationHandler
{
    private readonly IAuditSink _audit;

    public AuditSessionHandler(IAuditSink audit)
    {
        _audit = audit;
    }

    public async ValueTask OnSessionInitializedAsync(HttpContext httpContext, string sessionId, InitializeRequestParams initializeParams, CancellationToken ct)
    {
        await _audit.LogAsync(new AuditEvent(
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            Method: "initialize",
            ToolName: null,
            Outcome: "initialized",
            Reason: null), ct);
    }

    public ValueTask<InitializeRequestParams?> AllowSessionMigrationAsync(HttpContext httpContext, string sessionId, CancellationToken ct)
    {
        return new ValueTask<InitializeRequestParams?>((InitializeRequestParams?)null);
    }
}