using System.Text.Json;

namespace McpGuard.CapabilityCatalog;

public sealed record DiscoveredTool(string Name, string Description, JsonElement? InputSchema);