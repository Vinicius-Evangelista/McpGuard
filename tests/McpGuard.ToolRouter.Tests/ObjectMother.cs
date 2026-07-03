using System.Text.Json;
using McpGuard.Audit;
using McpGuard.ToolRegistry;
using McpGuard.ToolRouter;

namespace McpGuard.ToolRouter.Tests;

public sealed class ObjectMother
{
    public static ToolRegistration ApprovedEchoTool() => new(
        Name: "echo",
        Description: "Echoes the input message",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: true,
        Visible: true);

    public static ToolRegistration ApprovedAddTool() => new(
        Name: "add",
        Description: "Adds two integers",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: true,
        Visible: true);

    public static ToolRegistration DisallowedDangerousTool() => new(
        Name: "dangerous",
        Description: "A hidden, disallowed tool",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: false,
        Visible: false);

    public static ToolRegistration InvisibleTool() => new(
        Name: "secret",
        Description: "An allowed but invisible tool",
        DownstreamUrl: new Uri("http://localhost:5010/mcp"),
        Allowed: true,
        Visible: false);

    public static IReadOnlyList<ToolRegistration> AllToolRegistrations() =>
        [ApprovedEchoTool(), ApprovedAddTool(), DisallowedDangerousTool(), InvisibleTool()];

    public static JsonElement EchoCallArguments()
    {
        var json = """{"message":"hello"}""";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    public static RouteResult AllowedRouteResult() => new(true, "echo: hello", null);

    public static RouteResult BlockedRouteResult() => new(false, null, "tool 'dangerous' is not approved for execution");
}