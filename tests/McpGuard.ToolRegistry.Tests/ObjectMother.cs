using McpGuard.ToolRegistry;
using Microsoft.Extensions.Options;

namespace McpGuard.ToolRegistry.Tests;

public sealed class ObjectMother
{
    public static ToolRegistryOptions ToolRegistryOptions() => new()
    {
        Tools =
        [
            new ToolEntry
            {
                Name = "echo",
                Description = "Echoes the input message",
                DownstreamUrl = new Uri("http://localhost:5010/mcp"),
                Allowed = true,
                Visible = true
            },
            new ToolEntry
            {
                Name = "add",
                Description = "Adds two integers",
                DownstreamUrl = new Uri("http://localhost:5010/mcp"),
                Allowed = true,
                Visible = true
            },
            new ToolEntry
            {
                Name = "dangerous",
                Description = "A hidden, disallowed tool",
                DownstreamUrl = new Uri("http://localhost:5010/mcp"),
                Allowed = false,
                Visible = false
            }
        ]
    };

    public static IToolRegistry CreateRegistry() =>
        new ConfigToolRegistry(Options.Create(ToolRegistryOptions()));

    public static ToolRegistryOptions EmptyOptions() => new() { Tools = [] };
}