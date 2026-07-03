using System.Text.Json;
using McpGuard.Audit;

namespace McpGuard.Audit.Tests;

public sealed class ObjectMother
{
    public static AuditEvent InitializedEvent(string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:00Z"),
        SessionId: sessionId,
        Method: "initialize",
        ToolName: null,
        Outcome: "initialized",
        Reason: null);

    public static AuditEvent ToolsListedEvent(string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:01Z"),
        SessionId: sessionId,
        Method: "tools/list",
        ToolName: null,
        Outcome: "tools.listed",
        Reason: null);

    public static AuditEvent AllowedEvent(string toolName = "echo", string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:02Z"),
        SessionId: sessionId,
        Method: "tools/call",
        ToolName: toolName,
        Outcome: "tools.call.allowed",
        Reason: null);

    public static AuditEvent BlockedEvent(string toolName = "dangerous", string sessionId = "test-session-1") => new(
        Timestamp: DateTimeOffset.Parse("2026-07-03T12:00:03Z"),
        SessionId: sessionId,
        Method: "tools/call",
        ToolName: toolName,
        Outcome: "tools.call.blocked",
        Reason: "tool 'dangerous' is not approved for execution");
}