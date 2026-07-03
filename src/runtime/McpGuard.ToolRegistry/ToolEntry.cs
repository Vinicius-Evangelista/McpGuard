namespace McpGuard.ToolRegistry;

public sealed class ToolEntry
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public Uri DownstreamUrl { get; init; } = new("http://localhost");
    public bool Allowed { get; init; } = true;
    public bool Visible { get; init; } = true;
}