namespace McpGuard.ServerRegistry;

public sealed class ServerEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public Uri DownstreamUrl { get; set; } = new("http://localhost");
    public bool Enabled { get; set; } = true;
    public string DiscoveryState { get; set; } = "pending"; // pending | discovery-ok | discovery-failed
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CapabilityEntity> Capabilities { get; set; } = new();
}