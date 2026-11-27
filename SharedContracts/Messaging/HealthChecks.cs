using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedContracts.Messaging
{
    /// <summary>/health check: can this app reach RabbitMQ? (spec §40, optional)</summary>
    public sealed class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly RabbitMqConnection _rabbit;

        public RabbitMqHealthCheck(RabbitMqConnection rabbit) => _rabbit = rabbit;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                // Connecting is blocking; give up after a few seconds.
                var open = await Task.Run(() => _rabbit.GetConnection().IsOpen, ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
                return open ? HealthCheckResult.Healthy("Connected.") : HealthCheckResult.Unhealthy("Not connected.");
            }
            catch (Exception)
            {
                return HealthCheckResult.Unhealthy("Cannot reach RabbitMQ.");
            }
        }
    }

    /// <summary>Writes the /health answer as short JSON (no error details).</summary>
    public static class HealthResponse
    {
        public static Task WriteAsync(HttpContext http, HealthReport report)
        {
            http.Response.ContentType = "application/json";
            return http.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.ToDictionary(e => e.Key, e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description
                })
            }));
        }
    }
}
