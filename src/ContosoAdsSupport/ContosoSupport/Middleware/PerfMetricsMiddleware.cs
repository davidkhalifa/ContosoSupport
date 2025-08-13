using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Services;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ContosoSupport.Middleware
{
    public class PerfMetricsMiddleware
    {
        private static readonly Histogram<long> LatencyHistogram = TelemetryHelper.Meter.CreateHistogram<long>("ResponseLatencyMs");

        private readonly ILogger logger;
        private readonly RequestDelegate next;
        private readonly string location;
        private string? tenantId;

        public PerfMetricsMiddleware(
            ILogger<PerfMetricsMiddleware> logger,
            IVmMetadataService vmMetadataService,
            RequestDelegate next)
        {
            this.logger = logger;
            this.next = next;

            location = vmMetadataService.GetComputeLocationAsync().Result;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var timer = Stopwatch.StartNew();
            try
            {
                await next.Invoke(context).ConfigureAwait(false);
            }
            finally
            {
                timer.Stop();

                var action = context.Request.RouteValues["action"]?.ToString() ?? string.Empty;
                var controller = context.Request.RouteValues["controller"]?.ToString() ?? string.Empty;

                RecordLatencyMeasure(
                    latencyMeasurement: timer.ElapsedMilliseconds,
                    host: context.Request.Host.Host?.ToString() ?? "null",
                    subscriptionId: context.Request.RouteValues["subscriptionId"]?.ToString() ?? string.Empty,
                    resourceGroup: context.Request.RouteValues["resourceGroup"]?.ToString() ?? string.Empty,
                    resourceId: context.Request.RouteValues["resourceId"]?.ToString() ?? string.Empty,
                    method: context.Request.Method,
                    statusCode: context.Response.StatusCode,
                    controller,
                    action);
            }
        }

        private void RecordLatencyMeasure(
            long latencyMeasurement,
            string host,
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            string method,
            int statusCode,
            string controller,
            string action)
        {
            // Tenant would typically refer to a deployment, here we're generating a sample value
            string tenant = tenantId ??= $"{location}PrdCSP{host?.Split('.', StringSplitOptions.RemoveEmptyEntries)[0]}";

            string customerResourceId = !string.IsNullOrWhiteSpace(subscriptionId)
                ? $"subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Contoso.Support/ticketingSystem/{resourceId}"
                : "null";

            string operationId = !string.IsNullOrWhiteSpace(controller) ? $"{controller}.{action}" : "null";

            var latencyDimensions = new TagList()
            {
                { "LocationId", location },
                { "Tenant", tenant },
                { "CustomerResourceId", customerResourceId },
                { "HttpMethod", method },
                { "HttpStatusCode", statusCode },
                { "Operation", operationId },
            };

            LatencyHistogram.Record(latencyMeasurement, in latencyDimensions);
        }
    }
}
