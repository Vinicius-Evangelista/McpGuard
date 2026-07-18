using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace McpGuard.ServerRegistry;

internal sealed class McpDbContextFactory : IDesignTimeDbContextFactory<McpDbContext>
{
    public McpDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<McpDbContext>()
            .UseSqlite("Data Source=mcpguard.db")
            .Options;
        return new McpDbContext(options);
    }
}