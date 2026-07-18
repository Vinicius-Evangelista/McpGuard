using McpGuard.CapabilityCatalog;
using McpGuard.ServerRegistry;
using Microsoft.EntityFrameworkCore;

namespace McpGuard.Admin.Api;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/servers");

        group.MapPost("/", async (IDbContextFactory<McpDbContext> factory, ICapabilityDiscoverer discoverer, RegisterServerRequest request, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Name is required" });
            }

            if (!Uri.TryCreate(request.DownstreamUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                return Results.BadRequest(new { error = "DownstreamUrl must be an absolute http or https URL" });
            }

            var server = new ServerEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = request.Name,
                DownstreamUrl = uri,
                Enabled = request.Enabled ?? true,
                DiscoveryState = "pending",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await using (var db = await factory.CreateDbContextAsync(ct))
            {
                db.Servers.Add(server);
                await db.SaveChangesAsync(ct);
            }

            try
            {
                var tools = await discoverer.DiscoverAsync(uri, ct);

                await using var db = await factory.CreateDbContextAsync(ct);
                var tracked = await db.Servers.Include(s => s.Capabilities).FirstAsync(s => s.Id == server.Id, ct);
                tracked.Capabilities.Clear();
                var now = DateTimeOffset.UtcNow;
                foreach (var tool in tools)
                {
                    tracked.Capabilities.Add(new CapabilityEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ServerId = tracked.Id,
                        ToolName = tool.Name,
                        Description = tool.Description ?? "",
                        InputSchemaJson = tool.InputSchema.HasValue ? tool.InputSchema.Value.GetRawText() : null,
                        Allowed = true,
                        Visible = true,
                        SyncedAt = now
                    });
                }
                tracked.DiscoveryState = "discovery-ok";
                tracked.UpdatedAt = now;
                await db.SaveChangesAsync(ct);

                return Results.Created($"/servers/{tracked.Id}", tracked.ToDto());
            }
            catch (Exception)
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                var tracked = await db.Servers.FindAsync([server.Id], ct);
                if (tracked is not null)
                {
                    tracked.DiscoveryState = "discovery-failed";
                    tracked.UpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                }

                var dto = tracked?.ToDto() ?? server.ToDto();
                return new DiscoveryFailedResult($"/servers/{server.Id}", dto);
            }
        });

        group.MapGet("/", async (IDbContextFactory<McpDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var servers = await db.Servers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
            return Results.Ok(servers.Select(s => s.ToDto()));
        });

        group.MapGet("/{id}", async (IDbContextFactory<McpDbContext> factory, string id, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var server = await db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            return server is null ? Results.NotFound() : Results.Ok(server.ToDto());
        });

        group.MapPut("/{id}", async (IDbContextFactory<McpDbContext> factory, string id, UpdateServerRequest request, CancellationToken ct) =>
        {
            Uri? uri = null;
            if (request.DownstreamUrl is not null)
            {
                if (!Uri.TryCreate(request.DownstreamUrl, UriKind.Absolute, out uri) ||
                    uri.Scheme is not ("http" or "https"))
                {
                    return Results.BadRequest(new { error = "DownstreamUrl must be an absolute http or https URL" });
                }
            }

            await using var db = await factory.CreateDbContextAsync(ct);
            var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (server is null)
            {
                return Results.NotFound();
            }

            if (request.Name is not null)
            {
                server.Name = request.Name;
            }
            if (uri is not null)
            {
                server.DownstreamUrl = uri;
            }
            server.Enabled = request.Enabled;
            server.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(server.ToDto());
        });

        group.MapDelete("/{id}", async (IDbContextFactory<McpDbContext> factory, string id, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (server is null)
            {
                return Results.NotFound();
            }
            db.Servers.Remove(server);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/{id}/resync", async (IDbContextFactory<McpDbContext> factory, ICapabilityDiscoverer discoverer, string id, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var server = await db.Servers.Include(s => s.Capabilities).FirstOrDefaultAsync(s => s.Id == id, ct);
            if (server is null)
            {
                return Results.NotFound();
            }

            try
            {
                var tools = await discoverer.DiscoverAsync(id, ct);
                server.Capabilities.Clear();
                var now = DateTimeOffset.UtcNow;
                foreach (var tool in tools)
                {
                    server.Capabilities.Add(new CapabilityEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ServerId = server.Id,
                        ToolName = tool.Name,
                        Description = tool.Description ?? "",
                        InputSchemaJson = tool.InputSchema.HasValue ? tool.InputSchema.Value.GetRawText() : null,
                        Allowed = true,
                        Visible = true,
                        SyncedAt = now
                    });
                }
                server.DiscoveryState = "discovery-ok";
                server.UpdatedAt = now;
                await db.SaveChangesAsync(ct);

                return Results.Ok(new ResyncResultDto(server.Id, server.Capabilities.Count, now, Warning: null));
            }
            catch (Exception)
            {
                server.DiscoveryState = "discovery-failed";
                server.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                return new ResyncFailedResult(new ResyncResultDto(server.Id, 0, server.UpdatedAt, Warning: "discovery failed"));
            }
        });

        group.MapGet("/{id}/capabilities", async (IDbContextFactory<McpDbContext> factory, string id, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var server = await db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (server is null)
            {
                return Results.NotFound();
            }
            var caps = await db.Capabilities.AsNoTracking()
                .Where(c => c.ServerId == id)
                .OrderBy(c => c.ToolName)
                .ToListAsync(ct);
            return Results.Ok(caps.Select(c => c.ToDto()));
        });

        var capabilities = app.MapGroup("/capabilities");

        capabilities.MapGet("/", async (IDbContextFactory<McpDbContext> factory, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var caps = await db.Capabilities.AsNoTracking().OrderBy(c => c.ToolName).ToListAsync(ct);
            return Results.Ok(caps.Select(c => c.ToDto()));
        });

        capabilities.MapGet("/{id}", async (IDbContextFactory<McpDbContext> factory, string id, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var cap = await db.Capabilities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            return cap is null ? Results.NotFound() : Results.Ok(cap.ToDto());
        });

        capabilities.MapPatch("/{id}", async (IDbContextFactory<McpDbContext> factory, string id, PatchCapabilityRequest request, CancellationToken ct) =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var cap = await db.Capabilities.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cap is null)
            {
                return Results.NotFound();
            }
            if (request.Allowed.HasValue)
            {
                cap.Allowed = request.Allowed.Value;
            }
            if (request.Visible.HasValue)
            {
                cap.Visible = request.Visible.Value;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(cap.ToDto());
        });

        return app;
    }

    private sealed class DiscoveryFailedResult : IResult
    {
        private readonly string _location;
        private readonly ServerDto _dto;

        public DiscoveryFailedResult(string location, ServerDto dto)
        {
            _location = location;
            _dto = dto;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status201Created;
            httpContext.Response.Headers.Location = _location;
            httpContext.Response.Headers["X-Discovery-Warning"] = "discovery failed";
            await httpContext.Response.WriteAsJsonAsync(_dto);
        }
    }

    private sealed class ResyncFailedResult : IResult
    {
        private readonly ResyncResultDto _dto;

        public ResyncFailedResult(ResyncResultDto dto)
        {
            _dto = dto;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.Headers["X-Discovery-Warning"] = "discovery failed";
            await httpContext.Response.WriteAsJsonAsync(_dto);
        }
    }
}