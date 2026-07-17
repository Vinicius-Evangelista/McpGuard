using McpGuard.ServerRegistry;

namespace McpGuard.Admin.Api;

public sealed record RegisterServerRequest(string Name, string DownstreamUrl, bool? Enabled = null);

public sealed record UpdateServerRequest(string? Name, string? DownstreamUrl, bool Enabled);

public sealed record ServerDto(
    string Id,
    string Name,
    string DownstreamUrl,
    bool Enabled,
    string DiscoveryState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ResyncResultDto(string ServerId, int CapabilitiesCount, DateTimeOffset SyncedAt);

public sealed record PatchCapabilityRequest(bool? Allowed, bool? Visible);

public sealed record CapabilityDto(
    string Id,
    string ServerId,
    string ToolName,
    string Description,
    bool Allowed,
    bool Visible,
    DateTimeOffset SyncedAt);

public static class ServerDtoMapper
{
    public static ServerDto ToDto(this ServerEntity server) => new(
        server.Id,
        server.Name,
        server.DownstreamUrl.ToString(),
        server.Enabled,
        server.DiscoveryState,
        server.CreatedAt,
        server.UpdatedAt);
}

public static class CapabilityDtoMapper
{
    public static CapabilityDto ToDto(this CapabilityEntity capability) => new(
        capability.Id,
        capability.ServerId,
        capability.ToolName,
        capability.Description,
        capability.Allowed,
        capability.Visible,
        capability.SyncedAt);
}