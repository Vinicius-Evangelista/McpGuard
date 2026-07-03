using Microsoft.Extensions.Options;

namespace McpGuard.ToolRegistry;

public sealed class ConfigToolRegistry : IToolRegistry
{
    private readonly IReadOnlyList<ToolRegistration> _tools;

    public ConfigToolRegistry(IOptions<ToolRegistryOptions> options)
    {
        _tools = options.Value.Tools
            .Select(e => new ToolRegistration(
                e.Name,
                e.Description,
                e.DownstreamUrl,
                e.Allowed,
                e.Visible))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ToolRegistration> GetAll(CancellationToken ct) => _tools;

    public ToolRegistration? Get(string name, CancellationToken ct) =>
        _tools.FirstOrDefault(t => t.Name == name);
}