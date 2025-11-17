using EMS.WebApp.Services;

namespace EMS.WebApp.Extensions
{
    public static class AuditExtensions
    {
        public static async Task AuditCreateAsync<T>(this IAuditService auditService, T entity, string tableName, string recordId, string? additionalInfo = null)
        {
            await auditService.LogCreateAsync(tableName, recordId, entity, additionalInfo);
        }

        public static async Task AuditUpdateAsync<T>(this IAuditService auditService, T oldEntity, T newEntity, string tableName, string recordId, string? additionalInfo = null)
        {
            await auditService.LogUpdateAsync(tableName, recordId, oldEntity, newEntity, additionalInfo);
        }

        public static async Task AuditDeleteAsync<T>(this IAuditService auditService, T entity, string tableName, string recordId, string? additionalInfo = null)
        {
            await auditService.LogDeleteAsync(tableName, recordId, entity, additionalInfo);
        }

        public static async Task AuditViewAsync(this IAuditService auditService, string tableName, string recordId, string? additionalInfo = null)
        {
            await auditService.LogViewAsync(tableName, recordId, additionalInfo);
        }
    }
}