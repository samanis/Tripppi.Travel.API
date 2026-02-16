using System.Net;
using System.Text.Json;

namespace Tripppi.Travel.API.Middleware
{
    /// <summary>
    /// Global exception handling middleware to ensure the application never crashes.
    /// Logs all exceptions and returns appropriate error responses based on environment.
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred. Request: {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            try
            {
                // Check if headers have already been sent - if so, we can't modify the response
                if (context.Response.HasStarted)
                {
                    _logger.LogError(exception, "Response has already started. Cannot send error response.");
                    return;
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var errorResponse = new ErrorResponse
                {
                    StatusCode = context.Response.StatusCode,
                    Message = "An error occurred while processing your request.",
                    Timestamp = DateTime.UtcNow
                };

                // Include exception details only in Development mode
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = exception.Message;
                    errorResponse.StackTrace = exception.StackTrace;
                    errorResponse.ExceptionType = exception.GetType().Name;
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = _environment.IsDevelopment()
                };

                var json = JsonSerializer.Serialize(errorResponse, jsonOptions);
                await context.Response.WriteAsync(json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while handling an exception");
                // If we can't even write the error response, just log it and give up
            }
        }

        private class ErrorResponse
        {
            public int StatusCode { get; set; }
            public string Message { get; set; } = null!;
            public DateTime Timestamp { get; set; }
            public string? Details { get; set; }
            public string? StackTrace { get; set; }
            public string? ExceptionType { get; set; }
        }
    }
}
