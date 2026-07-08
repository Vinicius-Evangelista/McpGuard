using McpGuard.ToolRegistry;
using Microsoft.Extensions.Options;
using Xunit;

namespace McpGuard.ToolRegistry.Tests;

public sealed class Config_tool_registry
{
    [Fact]
    public void Returns_only_configured_tools()
    {
        var registry = CreateRegistry();
        var tools = registry.GetAll(CancellationToken.None);

        Assert.Equal(3, tools.Count);
        Assert.Equal("echo", tools[0].Name);
        Assert.Equal("add", tools[1].Name);
        Assert.Equal("dangerous", tools[2].Name);
    }

    [Fact]
    public void Returns_null_for_unknown_tool()
    {
        var registry = CreateRegistry();
        var result = registry.Get("nonexistent", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void Preserves_allowed_and_visible_flags()
    {
        var registry = CreateRegistry();
        var tools = registry.GetAll(CancellationToken.None);

        var echo = tools.First(t => t.Name == "echo");
        Assert.True(echo.Allowed);
        Assert.True(echo.Visible);

        var dangerous = tools.First(t => t.Name == "dangerous");
        Assert.False(dangerous.Allowed);
        Assert.False(dangerous.Visible);
    }

    [Fact]
    public void Maps_downstream_url()
    {
        var registry = CreateRegistry();
        var echo = registry.Get("echo", CancellationToken.None)!;

        Assert.Equal("http://localhost:5010/mcp", echo.DownstreamUrl.ToString());
    }

    [Fact]
    public void Returns_empty_list_when_no_tools_configured()
    {
        var registry = new ConfigToolRegistry(Options.Create(EmptyOptions()));
        var tools = registry.GetAll(CancellationToken.None);

        Assert.Empty(tools);
    }

    private static ToolRegistryOptions ToolRegistryOptions() => new()
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

    private static IToolRegistry CreateRegistry() =>
        new ConfigToolRegistry(Options.Create(ToolRegistryOptions()));

    private static ToolRegistryOptions EmptyOptions() => new() { Tools = [] };
}