namespace McpGuard.ToolRegistry;

public sealed record ToolRegistration(
    string Name,
    string Description,
    Uri DownstreamUrl,
    bool Allowed,
    bool Visible);