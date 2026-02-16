using Microsoft.Extensions.Logging;

namespace Tripppi.Travel.API.Middleware
{
    /// <summary>
    /// Validates that requests come from CloudFront by checking a custom origin header.
    /// This prevents direct access to the ALB/API from the public internet.
    /// Health check endpoints are exempt to allow ALB health probes.
    /// </summary>
    public class OriginSecretValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string? _expectedSecret;
        private readonly ILogger<OriginSecretValidationMiddleware> _logger;
        private readonly bool _isEnabled;

        public OriginSecretValidationMiddleware(
            RequestDelegate next,
            IConfiguration config,
            ILogger<OriginSecretValidationMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _expectedSecret = config["Security:OriginSecret"];
            
            // Allow disabling in local development
            _isEnabled = !string.IsNullOrWhiteSpace(_expectedSecret);
            
            if (!_isEnabled)
            {
                _logger.LogWarning("OriginSecretValidationMiddleware is DISABLED (no Security:OriginSecret configured). Do not deploy to production without this.");
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip validation if disabled (local dev)
            if (!_isEnabled)
            {
                await _next(context);
                return;
            }

            // Skip validation for health check endpoints (ALB needs direct access)
            if (context.Request.Path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Validate origin header
            if (!context.Request.Headers.TryGetValue("X-Origin-Secret", out var headerValue) ||
                !string.Equals(headerValue, _expectedSecret, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Request blocked: missing or invalid origin secret. Path: {Path}, IP: {IP}",
                    context.Request.Path,
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "Forbidden" });
                return;
            }

            await _next(context);
        }
    }
}
