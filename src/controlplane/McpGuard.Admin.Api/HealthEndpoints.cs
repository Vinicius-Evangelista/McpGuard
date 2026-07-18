using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace McpGuard.Admin.Api;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapAdminHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthResponseWriter.WriteHealthResponseAsync
        });

        return endpoints;
    }
}