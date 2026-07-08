using System.Text.Json;
using McpGuard.Audit;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace McpGuard.Audit.Tests;

public sealed class Logger_audit_sink
{
    private readonly ITestOutputHelper _output;

    public Logger_audit_sink(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Writes_one_json_line_per_event()
    {
        var (logger, provider) = CreateSink();
        var sink = new LoggerAuditSink(logger);

        var evt = InitializedEvent();
        await sink.LogAsync(evt, CancellationToken.None);

        Assert.Single(provider.GetLogLines());
    }

    [Fact]
    public async Task Serializes_all_fields_in_camel_case()
    {
        var (logger, provider) = CreateSink();
        var sink = new LoggerAuditSink(logger);

        var evt = AllowedEvent();
        await sink.LogAsync(evt, CancellationToken.None);

        var json = provider.GetLogLines()[0];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("timestamp", out _));
        Assert.True(root.TryGetProperty("sessionId", out _));
        Assert.True(root.TryGetProperty("method", out _));
        Assert.True(root.TryGetProperty("toolName", out _));
        Assert.True(root.TryGetProperty("outcome", out _));
        Assert.Equal("tools/call", root.GetProperty("method").GetString());
        Assert.Equal("echo", root.GetProperty("toolName").GetString());
        Assert.Equal("tools.call.allowed", root.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Includes_reason_when_blocked()
    {
        var (logger, provider) = CreateSink();
        var sink = new LoggerAuditSink(logger);

        var evt = BlockedEvent();
        await sink.LogAsync(evt, CancellationToken.None);

        var json = provider.GetLogLines()[0];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("reason", out var reason));
        Assert.Equal("tool 'dangerous' is not approved for execution", reason.GetString());
    }

    [Fact]
    public async Task Omits_reason_when_not_blocked()
    {
        var (logger, provider) = CreateSink();
        var sink = new LoggerAuditSink(logger);

        var evt = InitializedEvent();
        await sink.LogAsync(evt, CancellationToken.None);

        var json = provider.GetLogLines()[0];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("reason", out _));
    }

    private static (ILogger<LoggerAuditSink> logger, CapturingLoggerProvider provider) CreateSink()
    {
        var provider = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = factory.CreateLogger<LoggerAuditSink>();
        return (logger, provider);
    }

    private static AuditEvent InitializedEvent(string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:00Z"),
        SessionId: sessionId,
        Method: "initialize",
        ToolName: null,
        Outcome: "initialized",
        Reason: null);

    private static AuditEvent AllowedEvent(string toolName = "echo", string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:02Z"),
        SessionId: sessionId,
        Method: "tools/call",
        ToolName: toolName,
        Outcome: "tools.call.allowed",
        Reason: null);

    private static AuditEvent BlockedEvent(string toolName = "dangerous", string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:03Z"),
        SessionId: sessionId,
        Method: "tools/call",
        ToolName: toolName,
        Outcome: "tools.call.blocked",
        Reason: "tool 'dangerous' is not approved for execution");

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _lines = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);

        public void Dispose() { }

        public List<string> GetLogLines() => _lines;

        private sealed class CapturingLogger(List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lines.Add(formatter(state, exception));
            }
        }
    }
}