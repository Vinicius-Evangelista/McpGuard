using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpGuard.SampleTools.Server.Tools;

[McpServerToolType]
public sealed class AddTool
{
    [McpServerTool(Name = "add")]
    [Description("Adds two integers and returns the result.")]
    public static int Add(int a, int b) => a + b;
}