using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IAuditLogRepository
    {
        Task<IEnumerable<SysAuditLog>> ListAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 100);
        Task<int> CountAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<SysAuditLog?> GetByIdAsync(long auditId);
        Task<IEnumerable<string>> GetDistinctTableNamesAsync();
        Task<IEnumerable<string>> GetDistinctActionTypesAsync();
        Task<IEnumerable<SysAuditLog>> GetEntityHistoryAsync(string tableName, string recordId);
    }
}