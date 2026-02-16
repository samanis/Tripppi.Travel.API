using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Tripppi.Travel.API.Middleware
{
    /// <summary>
    /// Logs every incoming HTTP request and outgoing response.
    /// CRITICAL: Does NOT log request/response bodies that may contain PII.
    /// Only logs: method, path, query, status code, timing, and safe headers.
    /// </summary>
    public sealed class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N")[..8]; // Short request ID for correlation
            
            // Log incoming request (no body to avoid PII)
            LogRequest(context, requestId);

            // Capture original response body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                // Continue processing
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log exception but let GlobalExceptionHandlerMiddleware handle it
                _logger.LogError(ex, "[{RequestId}] Unhandled exception during request processing: {Method} {Path}", 
                    requestId, context.Request.Method, context.Request.Path);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                
                // Log response (no body to avoid PII)
                LogResponse(context, requestId, stopwatch.ElapsedMilliseconds);
                
                context.Response.Body = originalBodyStream;
            }
        }

        private void LogRequest(HttpContext context, string requestId)
        {
            var request = context.Request;
            var queryString = request.QueryString.HasValue ? request.QueryString.Value : string.Empty;
            
            // Safe headers only (no Authorization, Cookie, etc.)
            var userAgent = request.Headers.UserAgent.ToString();
            var contentType = request.ContentType ?? "none";
            
            _logger.LogInformation(
                "[{RequestId}] --> {Method} {Path}{Query} | ContentType={ContentType} | UserAgent={UserAgent} | RemoteIp={RemoteIp}",
                requestId,
                request.Method,
                request.Path,
                queryString,
                contentType,
                userAgent,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            );
        }

        private void LogResponse(HttpContext context, string requestId, long elapsedMs)
        {
            var response = context.Response;
            
            _logger.LogInformation(
                "[{RequestId}] <-- {StatusCode} | {ElapsedMs}ms | ContentType={ContentType}",
                requestId,
                response.StatusCode,
                elapsedMs,
                response.ContentType ?? "none"
            );
        }
    }
}
