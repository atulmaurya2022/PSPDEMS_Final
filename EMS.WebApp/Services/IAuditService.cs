using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IAuditService
    {
        Task LogAsync(string tableName, string actionType, string recordId, object? oldValues, object? newValues, string? additionalInfo = null);
        Task LogCreateAsync<T>(string tableName, string recordId, T newEntity, string? additionalInfo = null);
        Task LogUpdateAsync<T>(string tableName, string recordId, T oldEntity, T newEntity, string? additionalInfo = null);
        Task LogDeleteAsync<T>(string tableName, string recordId, T deletedEntity, string? additionalInfo = null);
        Task LogViewAsync(string tableName, string recordId, string? additionalInfo = null);
        Task<IEnumerable<SysAuditLog>> GetAuditLogsAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 100);
        Task<int> GetAuditLogsCountAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null);
    }
}
