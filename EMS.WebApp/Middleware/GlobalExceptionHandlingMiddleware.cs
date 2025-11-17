using EMS.WebApp.Services;

namespace EMS.WebApp.Middleware
{
    public class BasicErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<BasicErrorHandlingMiddleware> _logger;

        public BasicErrorHandlingMiddleware(RequestDelegate next, ILogger<BasicErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);

                // Check for status codes that need custom handling (like 401, 403, 404)
                // This runs AFTER authentication middleware has set the status code
                if (context.Response.StatusCode >= 400 && !context.Response.HasStarted)
                {
                    await HandleStatusCodeAsync(context, context.Response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleStatusCodeAsync(HttpContext context, int statusCode)
        {
            // Don't handle if response has already started
            if (context.Response.HasStarted)
                return;

            _logger.LogWarning("HTTP {StatusCode} response for path: {Path}", statusCode, context.Request.Path);

            // Add security headers
            if (!context.Response.Headers.ContainsKey("Cache-Control"))
            {
                context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                context.Response.Headers.Append("Pragma", "no-cache");
                context.Response.Headers.Append("Expires", "0");
            }

            // For API requests, return JSON error
            if (IsApiRequest(context))
            {
                await HandleApiStatusCodeAsync(context, statusCode);
            }
            else
            {
                // For web requests, redirect to error page
                context.Response.Redirect($"/Error/{statusCode}");
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Get services
            var errorHandlingService = context.RequestServices.GetService<IErrorHandlingService>();

            // Get client info
            var clientIp = GetClientIpAddress(context);
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var path = context.Request.Path.ToString();

            // Create additional info
            var additionalInfo = $"IP: {clientIp}, UserAgent: {userAgent}, Path: {path}";

            // Log the error
            if (errorHandlingService != null)
            {
                await errorHandlingService.LogErrorAsync(exception, additionalInfo);
            }
            else
            {
                _logger.LogError(exception, "Unhandled exception occurred. {AdditionalInfo}", additionalInfo);
            }

            // Determine status code using if-else instead of switch expression
            var statusCode = GetStatusCodeFromException(exception);

            // Set response status
            context.Response.StatusCode = statusCode;

            // Add security headers
            context.Response.Headers.Remove("Cache-Control");
            context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            context.Response.Headers.Append("Pragma", "no-cache");
            context.Response.Headers.Append("Expires", "0");

            // For API requests (JSON), return JSON error
            if (IsApiRequest(context))
            {
                await HandleApiErrorAsync(context, exception, statusCode);
            }
            else
            {
                // For web requests, redirect to error page
                context.Response.Redirect($"/Error/{statusCode}");
            }
        }

        private static async Task HandleApiStatusCodeAsync(HttpContext context, int statusCode)
        {
            context.Response.ContentType = "application/json";

            var errorMessage = GetUserFriendlyMessage(statusCode);
            var requestId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

            var errorResponse = new
            {
                error = new
                {
                    statusCode = statusCode,
                    message = errorMessage,
                    requestId = requestId,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
        }

        private static int GetStatusCodeFromException(Exception exception)
        {
            if (exception is ArgumentException || exception is ArgumentNullException)
                return 400;
            if (exception is UnauthorizedAccessException)
                return 401;
            if (exception is FileNotFoundException)
                return 404;
            if (exception is TimeoutException)
                return 408;
            if (exception is InvalidOperationException)
                return 409;
            if (exception is NotSupportedException)
                return 415;
            if (exception is NotImplementedException)
                return 501;

            return 500; // Default to internal server error
        }

        private static bool IsApiRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments("/api") ||
                   context.Request.Headers["Accept"].Any(h => h.Contains("application/json")) ||
                   context.Request.ContentType?.Contains("application/json") == true;
        }

        private static async Task HandleApiErrorAsync(HttpContext context, Exception exception, int statusCode)
        {
            context.Response.ContentType = "application/json";

            var errorMessage = GetUserFriendlyMessage(statusCode);
            var requestId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

            var errorResponse = new
            {
                error = new
                {
                    statusCode = statusCode,
                    message = errorMessage,
                    requestId = requestId,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
        }

        private static string GetUserFriendlyMessage(int statusCode)
        {
            if (statusCode == 400) return "Invalid request parameters.";
            if (statusCode == 401) return "Unauthorized access.";
            if (statusCode == 403) return "Access forbidden.";
            if (statusCode == 404) return "Requested resource not found.";
            if (statusCode == 408) return "Request timeout.";
            if (statusCode == 409) return "Invalid operation.";
            if (statusCode == 415) return "Operation not supported.";
            if (statusCode == 501) return "Feature not implemented.";

            return "An internal server error occurred.";
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();

            if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    ipAddress = forwardedFor.Split(',').FirstOrDefault()?.Trim();
                }
            }
            else if (context.Request.Headers.ContainsKey("X-Real-IP"))
            {
                var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIp))
                {
                    ipAddress = realIp;
                }
            }

            return ipAddress ?? "Unknown";
        }
    }

    public static class BasicErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseBasicErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<BasicErrorHandlingMiddleware>();
        }
    }
}