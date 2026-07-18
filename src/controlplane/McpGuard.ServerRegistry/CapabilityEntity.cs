namespace McpGuard.ServerRegistry;

public sealed class CapabilityEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ServerId { get; set; } = ""; // FK -> ServerEntity.Id
    public ServerEntity Server { get; set; } = null!; // nav
    public string ToolName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? InputSchemaJson { get; set; } // JSON-encoded input schema (nullable for tools without one)
    public bool Allowed { get; set; } = true;
    public bool Visible { get; set; } = true;
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
}