using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace McpGuard.Admin.Api;

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.StatusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";

        var servers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (report.Entries.TryGetValue("downstream", out var entry) && entry.Data is not null)
        {
            foreach (var (key, value) in entry.Data)
            {
                servers[key] = value?.ToString() ?? "unknown";
            }
        }

        var payload = new
        {
            status = report.Status.ToString(),
            servers
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions);
    }
}