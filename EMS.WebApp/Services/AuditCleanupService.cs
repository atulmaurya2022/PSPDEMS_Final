using EMS.WebApp.Configuration;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.WebApp.Services
{
    public interface IAuditCleanupService
    {
        Task CleanupOldAuditLogsAsync();
        Task<int> GetOldAuditLogsCountAsync();
    }

    public class AuditCleanupService : IAuditCleanupService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditSettings _auditSettings;
        private readonly ILogger<AuditCleanupService> _logger;

        public AuditCleanupService(
            ApplicationDbContext context,
            IOptions<AuditSettings> auditSettings,
            ILogger<AuditCleanupService> logger)
        {
            _context = context;
            _auditSettings = auditSettings.Value;
            _logger = logger;
        }

        public async Task CleanupOldAuditLogsAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-_auditSettings.MaxAuditRetentionDays);

                var oldLogs = await _context.SysAuditLogs
                    .Where(a => a.Timestamp < cutoffDate)
                    .ToListAsync();

                if (oldLogs.Any())
                {
                    _context.SysAuditLogs.RemoveRange(oldLogs);
                    var deletedCount = await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} old audit logs older than {CutoffDate}",
                        deletedCount, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old audit logs");
            }
        }

        public async Task<int> GetOldAuditLogsCountAsync()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_auditSettings.MaxAuditRetentionDays);
            return await _context.SysAuditLogs.CountAsync(a => a.Timestamp < cutoffDate);
        }
    }
}
