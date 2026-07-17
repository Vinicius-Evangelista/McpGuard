using System.Text.Json;

namespace McpGuard.ToolRegistry;

public sealed record ToolRegistration(
    string Name,
    string Description,
    Uri DownstreamUrl,
    bool Allowed,
    bool Visible,
    string? ServerId = null,
    JsonElement? InputSchema = null,
    string? CapabilityId = null);