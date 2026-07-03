using McpGuard.ToolRegistry;

namespace McpGuard.ToolRouter.Tests.Fakes;

public sealed class FakeToolRegistry : IToolRegistry
{
    private readonly IReadOnlyList<ToolRegistration> _tools;

    public FakeToolRegistry(IReadOnlyList<ToolRegistration> tools) => _tools = tools;

    public IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct) => _tools;

    public ToolRegistration? Get(string name, CancellationToken ct) =>
        _tools.FirstOrDefault(t => t.Name == name);
}