using System.Text.Json;
using McpGuard.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace McpGuard.ServerRegistry;

public sealed class StoreToolRegistry : IAsyncToolRegistry
{
    private readonly IDbContextFactory<McpDbContext> _factory;

    public StoreToolRegistry(IDbContextFactory<McpDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<ToolRegistration>> GetAllAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var servers = await db.Servers
            .Include(s => s.Capabilities)
            .Where(s => s.Enabled)
            .ToListAsync(ct);

        var tools = new List<ToolRegistration>();
        foreach (var server in servers)
        {
            foreach (var cap in server.Capabilities)
            {
                tools.Add(Map(server, cap));
            }
        }

        return tools;
    }

    public async Task<ToolRegistration?> GetAsync(string name, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var servers = await db.Servers
            .Include(s => s.Capabilities)
            .Where(s => s.Enabled)
            .ToListAsync(ct);

        var cap = servers
            .SelectMany(s => s.Capabilities)
            .FirstOrDefault(c => c.ToolName == name);

        if (cap is null)
        {
            return null;
        }

        var server = servers.First(s => s.Id == cap.ServerId);
        return Map(server, cap);
    }

    private static ToolRegistration Map(ServerEntity server, CapabilityEntity cap)
    {
        JsonElement? inputSchema = string.IsNullOrEmpty(cap.InputSchemaJson)
            ? null
            : JsonDocument.Parse(cap.InputSchemaJson).RootElement;

        return new ToolRegistration(
            Name: cap.ToolName,
            Description: cap.Description,
            DownstreamUrl: server.DownstreamUrl,
            Allowed: cap.Allowed,
            Visible: cap.Visible,
            ServerId: server.Id,
            InputSchema: inputSchema,
            CapabilityId: cap.Id);
    }
}