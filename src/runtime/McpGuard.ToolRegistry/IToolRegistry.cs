namespace McpGuard.ToolRegistry;

public interface IToolRegistry
{
    IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct);

    ToolRegistration? Get(string name, CancellationToken ct);
}