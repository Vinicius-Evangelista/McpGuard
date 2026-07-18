namespace McpGuard.ToolRegistry;

public interface IAsyncToolRegistry
{
    Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken ct);

    Task<ToolRegistration?> GetAsync(string name, CancellationToken ct);
}