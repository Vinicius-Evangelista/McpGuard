using McpGuard.ToolRegistry;

namespace McpGuard.ToolRouter.Tests.Fakes;

public sealed class FakeAsyncToolRegistry : IAsyncToolRegistry
{
    private readonly IReadOnlyList<ToolRegistration> _tools;

    public FakeAsyncToolRegistry(IReadOnlyList<ToolRegistration> tools) => _tools = tools;

    public Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult(_tools);

    public Task<ToolRegistration?> GetAsync(string name, CancellationToken ct) =>
        Task.FromResult(_tools.FirstOrDefault(t => t.Name == name));
}