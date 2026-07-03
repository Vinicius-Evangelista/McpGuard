namespace McpGuard.ToolRouter;

public sealed record RouteResult(bool Allowed, object? Result, string? BlockReason);