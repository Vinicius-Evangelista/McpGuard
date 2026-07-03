using McpGuard.Audit;

namespace McpGuard.ToolRouter.Tests.Fakes;

public sealed class FakeAuditSink : IAuditSink
{
    private readonly List<AuditEvent> _events = [];

    public Task LogAsync(AuditEvent evt, CancellationToken ct)
    {
        _events.Add(evt);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AuditEvent> GetEvents() => _events.AsReadOnly();
}