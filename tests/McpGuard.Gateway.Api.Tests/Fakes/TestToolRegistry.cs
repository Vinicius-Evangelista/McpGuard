using McpGuard.ToolRegistry;

namespace McpGuard.Gateway.Api.Tests.Fakes;

public sealed class TestToolRegistry : IToolRegistry, IAsyncToolRegistry
{
    private readonly IReadOnlyList<ToolRegistration> _tools;

    public TestToolRegistry(IReadOnlyList<ToolRegistration> tools)
    {
        _tools = tools;
    }

    public IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct) => _tools;

    public ToolRegistration? Get(string name, CancellationToken ct) =>
        _tools.FirstOrDefault(t => t.Name == name);

    public Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken ct) => Task.FromResult(GetAll(ct));

    public Task<ToolRegistration?> GetAsync(string name, CancellationToken ct) => Task.FromResult(Get(name, ct));
}