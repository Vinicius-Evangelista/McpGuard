using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpGuard.SampleTools.Server.Tools;

[McpServerToolType]
public sealed class EchoTool
{
    [McpServerTool(Name = "echo")]
    [Description("Echoes the input message back to the caller.")]
    public static string Echo(string message) => message;
}