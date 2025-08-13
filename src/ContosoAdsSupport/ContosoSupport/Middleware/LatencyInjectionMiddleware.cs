using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ContosoSupport.Middleware
{
    public class LatencyInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LatencyInjectionMiddleware> _logger;
        private const int MaxDelayMs = 5000;
        private const string X_MS_ADDED_LATENCY_HEADER = "x-ms-added-latency";

        public LatencyInjectionMiddleware(RequestDelegate next, ILogger<LatencyInjectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(X_MS_ADDED_LATENCY_HEADER, out var headerValues))
            {
                var headerValue = headerValues[0];
                if (!int.TryParse(headerValue, out int delayMs) || delayMs < 0)
                {
                    _logger.LogWarning("Invalid value for latency header {Header}: {HeaderValue} from {RemoteIpAddress}", X_MS_ADDED_LATENCY_HEADER, headerValue, context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("x-ms-added-latency header has an invalid value.");
                    return;
                }

                if (delayMs > MaxDelayMs)
                {
                    _logger.LogWarning("Invalid latency value in header {Header}: {DelayMs}ms from {RemoteIpAddress}", X_MS_ADDED_LATENCY_HEADER, delayMs, context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("x-ms-added-latency header has an invalid value (must be <= 5000 ms).");
                    return;
                }

                if (delayMs > 0)
                {
                    _logger.LogInformation("Injecting latency of {DelayMs}ms for request {Method} {Path} from {RemoteIpAddress}", delayMs, context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
                    await Task.Delay(delayMs);
                }
            }

            await _next(context);
        }
    }
}
