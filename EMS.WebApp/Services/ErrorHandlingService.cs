using Microsoft.Extensions.Caching.Memory;
using EMS.WebApp.Services;

namespace EMS.WebApp.Services
{
    public interface IErrorHandlingService
    {
        Task LogErrorAsync(Exception exception, string additionalInfo = "");
        string GetUserFriendlyMessage(int statusCode);
        bool ShouldLogError(Exception exception);
        Task<bool> IsRateLimitedAsync(string clientIp);
    }

    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;
        private readonly IEncryptionService _encryptionService;

        public ErrorHandlingService(
            ILogger<ErrorHandlingService> logger,
            IConfiguration configuration,
            IMemoryCache memoryCache,
            IEncryptionService encryptionService)
        {
            _logger = logger;
            _configuration = configuration;
            _memoryCache = memoryCache;
            _encryptionService = encryptionService;
        }

        public async Task LogErrorAsync(Exception exception, string additionalInfo = "")
        {
            if (ShouldLogError(exception))
            {
                var errorId = Guid.NewGuid().ToString();

                var errorDetails = new
                {
                    ErrorId = errorId,
                    Exception = exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException?.Message,
                    AdditionalInfo = additionalInfo,
                    Timestamp = DateTime.UtcNow,
                    Environment = Environment.MachineName,
                    Application = "EMS.WebApp"
                };

                _logger.LogError(exception, "Error logged with ID: {ErrorId}. Details: {@ErrorDetails}", errorId, errorDetails);

                // Store error details in cache for potential retrieval (encrypted)
                var encryptedErrorDetails = _encryptionService.Encrypt(System.Text.Json.JsonSerializer.Serialize(errorDetails));
                _memoryCache.Set($"error_{errorId}", encryptedErrorDetails, TimeSpan.FromHours(24));

                // Here you could add integration with external logging services
                // like Application Insights, Sentry, or your custom logging infrastructure
                await Task.CompletedTask;
            }
        }

        public string GetUserFriendlyMessage(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request: The request was invalid. Please check your input and try again.",
                401 => "Unauthorized: Please log in to access this resource.",
                403 => "Forbidden: You don't have permission to access this resource.",
                404 => "Not Found: The page you're looking for could not be found.",
                405 => "Method Not Allowed: The requested method is not supported for this resource.",
                408 => "Request Timeout: The request took too long to process. Please try again.",
                413 => "Request Too Large: The uploaded file or data is too large.",
                415 => "Unsupported Media Type: The file type is not supported.",
                429 => "Too Many Requests: Please wait a moment and try again.",
                500 => "Internal Server Error: Something went wrong on our server. Please try again later.",
                502 => "Bad Gateway: The server received an invalid response.",
                503 => "Service Unavailable: The service is temporarily unavailable. Please try again later.",
                504 => "Gateway Timeout: The request took too long to process.",
                _ => "An unexpected error occurred. Please try again or contact support if the problem persists."
            };
        }

        public bool ShouldLogError(Exception exception)
        {
            // Don't log certain types of exceptions to reduce noise
            if (exception is OperationCanceledException or TaskCanceledException)
                return false;

            // Don't log common HTTP exceptions for certain status codes
            if (exception is HttpRequestException httpEx)
            {
                if (httpEx.Data.Contains("StatusCode"))
                {
                    var statusCode = httpEx.Data["StatusCode"]?.ToString();
                    if (statusCode is "404" or "401" or "403")
                        return false;
                }
            }

            // Don't log validation exceptions (they're handled by model validation)
            if (exception is ArgumentException or ArgumentNullException)
                return false;

            return true;
        }

        public async Task<bool> IsRateLimitedAsync(string clientIp)
        {
            if (string.IsNullOrEmpty(clientIp))
                return false;

            var cacheKey = $"error_rate_limit_{clientIp}";
            var currentCount = _memoryCache.Get<int>(cacheKey);

            // Allow up to 10 error requests per minute per IP
            const int maxErrorsPerMinute = 10;

            if (currentCount >= maxErrorsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}. Count: {Count}", clientIp, currentCount);
                return true;
            }

            // Increment counter
            _memoryCache.Set(cacheKey, currentCount + 1, TimeSpan.FromMinutes(1));

            await Task.CompletedTask;
            return false;
        }
    }

    // Extension method for service registration
    public static class ErrorHandlingServiceExtensions
    {
        public static IServiceCollection AddErrorHandling(this IServiceCollection services)
        {
            services.AddScoped<IErrorHandlingService, ErrorHandlingService>();
            return services;
        }
    }
}