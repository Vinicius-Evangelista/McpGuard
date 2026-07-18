using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace McpGuard.ServerRegistry;

public class McpDbContext : DbContext
{
    public DbSet<ServerEntity> Servers => Set<ServerEntity>();
    public DbSet<CapabilityEntity> Capabilities => Set<CapabilityEntity>();

    public McpDbContext(DbContextOptions<McpDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerEntity>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Id).HasMaxLength(32);
            b.Property(s => s.Name).IsRequired().HasMaxLength(256);
            b.Property(s => s.DownstreamUrl)
                .HasConversion(
                    u => u.ToString(),
                    s => new Uri(s, UriKind.RelativeOrAbsolute))
                .IsRequired();
            b.Property(s => s.Enabled).IsRequired();
            b.Property(s => s.DiscoveryState).IsRequired().HasMaxLength(32);
            b.Property(s => s.CreatedAt).IsRequired();
            b.Property(s => s.UpdatedAt).IsRequired();

            b.HasMany(s => s.Capabilities)
                .WithOne(c => c.Server)
                .HasForeignKey(c => c.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CapabilityEntity>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Id).HasMaxLength(32);
            b.Property(c => c.ServerId).IsRequired().HasMaxLength(32);
            b.Property(c => c.ToolName).IsRequired().HasMaxLength(256);
            b.Property(c => c.Description).IsRequired().HasMaxLength(2048);
            b.Property(c => c.Allowed).IsRequired();
            b.Property(c => c.Visible).IsRequired();
            b.Property(c => c.SyncedAt).IsRequired();

            b.HasIndex(c => new { c.ServerId, c.ToolName }).IsUnique();
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enable SQLite WAL mode on every connection open so concurrent readers don't block the
        // writer (gateway reads while Admin API writes). The interceptor is a no-op for non-SQLite
        // providers (e.g. the InMemory provider used in unit tests) — it checks the connection
        // type before issuing the PRAGMA.
        optionsBuilder.AddInterceptors(new SqliteWalInterceptor());
    }

    private sealed class SqliteWalInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(
            DbConnection connection,
            ConnectionEndEventData eventData)
        {
            EnableWal(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            EnableWal(connection);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private static void EnableWal(DbConnection connection)
        {
            if (connection is SqliteConnection sqlite)
            {
                using var cmd = sqlite.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }
        }
    }
}