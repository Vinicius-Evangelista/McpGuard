using ModelContextProtocol.Protocol;

namespace McpGuard.ToolRouter;

public sealed record RouteResult(bool Allowed, CallToolResult? Result, string? BlockReason);