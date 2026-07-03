using McpGuard.ToolRegistry;

namespace McpGuard.Gateway.Api.Tests.Fakes;

public sealed class TestToolRegistry : IToolRegistry
{
    private readonly IReadOnlyList<ToolRegistration> _tools;

    public TestToolRegistry(IReadOnlyList<ToolRegistration> tools)
    {
        _tools = tools;
    }

    public IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct) => _tools;

    public ToolRegistration? Get(string name, CancellationToken ct) =>
        _tools.FirstOrDefault(t => t.Name == name);
}