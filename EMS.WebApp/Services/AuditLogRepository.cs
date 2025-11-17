using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ApplicationDbContext _context;

        public AuditLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SysAuditLog>> ListAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 100)
        {
            var query = _context.SysAuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(tableName))
                query = query.Where(a => a.TableName == tableName);

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(a => a.ActionType == actionType);

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

        public async Task<int> CountAsync(string? tableName = null, string? actionType = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.SysAuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(tableName))
                query = query.Where(a => a.TableName == tableName);

            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(a => a.ActionType == actionType);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query.CountAsync();
        }

        public async Task<SysAuditLog?> GetByIdAsync(long auditId)
        {
            return await _context.SysAuditLogs.FindAsync(auditId);
        }

        public async Task<IEnumerable<string>> GetDistinctTableNamesAsync()
        {
            return await _context.SysAuditLogs
                .Select(a => a.TableName)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetDistinctActionTypesAsync()
        {
            return await _context.SysAuditLogs
                .Select(a => a.ActionType)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }

        public async Task<IEnumerable<SysAuditLog>> GetEntityHistoryAsync(string tableName, string recordId)
        {
            return await _context.SysAuditLogs
                .Where(a => a.TableName == tableName && a.RecordId == recordId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }
    }
}
