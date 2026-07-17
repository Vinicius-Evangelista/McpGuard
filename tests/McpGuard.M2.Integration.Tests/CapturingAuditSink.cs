using McpGuard.Audit;

namespace McpGuard.M2.Integration.Tests;

public sealed class CapturingAuditSink : IAuditSink
{
    private readonly List<AuditEvent> _events = [];
    private readonly object _lock = new();

    public Task LogAsync(AuditEvent evt, CancellationToken ct)
    {
        lock (_lock)
        {
            _events.Add(evt);
            Monitor.PulseAll(_lock);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<AuditEvent> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
            Monitor.PulseAll(_lock);
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> WaitForEvents(int count, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            lock (_lock)
            {
                if (_events.Count >= count)
                    return _events.ToList().AsReadOnly();
            }

            if (DateTime.UtcNow > deadline)
            {
                lock (_lock)
                {
                    throw new TimeoutException(
                        $"Expected {count} audit events but found {_events.Count} within {timeout.TotalSeconds}s. " +
                        $"Events: {string.Join(", ", _events.Select(e => $"{e.Method}:{e.Outcome}"))}");
                }
            }

            await Task.Delay(100);
        }
    }
}