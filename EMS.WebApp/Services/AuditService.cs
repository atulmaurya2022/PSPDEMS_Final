using EMS.WebApp.Configuration;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuditSettings _auditSettings;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IOptions<AuditSettings> auditSettings,
            ILogger<AuditService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _auditSettings = auditSettings.Value;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        //public async Task LogAsync(string tableName, string actionType, string recordId, object? oldValues, object? newValues, string? additionalInfo = null)
        //{
        //    try
        //    {
        //        // Check if audit logging is enabled
        //        if (!_auditSettings.EnableAuditLog)
        //            return;

        //        // Check if table is excluded
        //        if (_auditSettings.ExcludedTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
        //            return;

        //        // Check if view actions should be logged
        //        if (actionType.ToUpper() == "VIEW" && !_auditSettings.LogViewActions)
        //            return;

        //        // Sanitize sensitive data
        //        var sanitizedOldValues = SanitizeSensitiveData(oldValues);
        //        var sanitizedNewValues = SanitizeSensitiveData(newValues);

        //        var httpContext = _httpContextAccessor.HttpContext;
        //        var user = httpContext?.User;

        //        var auditLog = new SysAuditLog
        //        {
        //            TableName = tableName,
        //            ActionType = actionType.ToUpper(),
        //            RecordId = recordId,
        //            OldValues = sanitizedOldValues != null ? JsonSerializer.Serialize(sanitizedOldValues, _jsonOptions) : null,
        //            NewValues = sanitizedNewValues != null ? JsonSerializer.Serialize(sanitizedNewValues, _jsonOptions) : null,
        //            UserName = GetCurrentUserName(),
        //            UserId = user?.FindFirst("user_id")?.Value,
        //            IpAddress = _auditSettings.EnableIPAddressLogging ? GetClientIpAddress() : null,
        //            UserAgent = _auditSettings.EnableUserAgentLogging ? httpContext?.Request.Headers["User-Agent"].ToString() : null,
        //            Timestamp = GetISTDateTime(),
        //            ControllerAction = GetControllerAction(),
        //            AdditionalInfo = additionalInfo
        //        };

        //        _context.SysAuditLogs.Add(auditLog);
        //        await _context.SaveChangesAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Audit logging failed for table {TableName}, action {ActionType}, record {RecordId}",
        //            tableName, actionType, recordId);
        //    }
        //}

        private object? SanitizeSensitiveData(object? data)
        {
            if (data == null) return null;

            try
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var jsonDocument = JsonDocument.Parse(json);

                return SanitizeJsonElement(jsonDocument.RootElement);
            }
            catch
            {
                return data; // Return original if sanitization fails
            }
        }

        private object SanitizeJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        var key = prop.Name;
                        var value = prop.Value;

                        if (_auditSettings.SensitiveFields.Any(field =>
                            key.Contains(field, StringComparison.OrdinalIgnoreCase)))
                        {
                            dict[key] = "***SENSITIVE***";
                        }
                        else
                        {
                            dict[key] = SanitizeJsonElement(value);
                        }
                    }
                    return dict;

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(SanitizeJsonElement).ToArray();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.TryGetInt64(out var longVal) ? longVal : element.GetDouble();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task LogAsync(string tableName, string actionType, string recordId, object? oldValues, object? newValues, string? additionalInfo = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = httpContext?.User;

                var auditLog = new SysAuditLog
                {
                    TableName = tableName,
                    ActionType = actionType.ToUpper(),
                    RecordId = recordId,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, _jsonOptions) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues, _jsonOptions) : null,
                    UserName = GetCurrentUserName(),
                    UserId = user?.FindFirst("user_id")?.Value,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
                    Timestamp = GetISTDateTime(),
                    ControllerAction = GetControllerAction(),
                    AdditionalInfo = additionalInfo
                };

                _context.SysAuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't throw to avoid disrupting main business logic
                Console.WriteLine($"Audit logging failed: {ex.Message}");
            }
        }

        public async Task LogCreateAsync<T>(string tableName, string recordId, T newEntity, string? additionalInfo = null)
        {
            await LogAsync(tableName, "CREATE", recordId, null, newEntity, additionalInfo);
        }

        public async Task LogUpdateAsync<T>(string tableName, string recordId, T oldEntity, T newEntity, string? additionalInfo = null)
        {
            await LogAsync(tableName, "UPDATE", recordId, oldEntity, newEntity, additionalInfo);
        }

        public async Task LogDeleteAsync<T>(string tableName, string recordId, T deletedEntity, string? additionalInfo = null)
        {
            await LogAsync(tableName, "DELETE", recordId, deletedEntity, null, additionalInfo);
        }

        public async Task LogViewAsync(string tableName, string recordId, string? additionalInfo = null)
        {
            await LogAsync(tableName, "VIEW", recordId, null, null, additionalInfo);
        }

        public async Task<IEnumerable<SysAuditLog>> GetAuditLogsAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 100)
        {
            var query = _context.SysAuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(tableName))
                query = query.Where(a => a.TableName == tableName);

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(a => a.ActionType == actionType.ToUpper());

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> GetAuditLogsCountAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.SysAuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(tableName))
                query = query.Where(a => a.TableName == tableName);

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(a => a.ActionType == actionType.ToUpper());

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query.CountAsync();
        }

        private string GetCurrentUserName()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            return user?.Identity?.Name
                   ?? user?.FindFirst("name")?.Value
                   ?? user?.FindFirst("user_name")?.Value
                   ?? user?.FindFirst("email")?.Value
                   ?? user?.FindFirst("user_id")?.Value
                   ?? "System";
        }

        private string? GetClientIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // Check for forwarded IP first (in case behind proxy/load balancer)
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }

        private string? GetControllerAction()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            var routeData = httpContext.GetRouteData();
            if (routeData == null) return null;

            var controller = routeData.Values["controller"]?.ToString();
            var action = routeData.Values["action"]?.ToString();

            return !string.IsNullOrEmpty(controller) && !string.IsNullOrEmpty(action)
                ? $"{controller}/{action}"
                : null;
        }

        private DateTime GetISTDateTime()
        {
            var utcNow = DateTime.UtcNow;
            var istTimeZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, istTimeZone);
        }
    }
}
