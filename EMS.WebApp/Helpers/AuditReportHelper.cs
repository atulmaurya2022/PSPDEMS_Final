using EMS.WebApp.Services;

namespace EMS.WebApp.Helpers
{
    public class AuditReportHelper
    {
        private readonly IAuditLogRepository _auditRepo;

        public AuditReportHelper(IAuditLogRepository auditRepo)
        {
            _auditRepo = auditRepo;
        }

        public async Task<object> GetUserActivitySummaryAsync(string userName, DateTime startDate, DateTime endDate)
        {
            var auditLogs = await _auditRepo.ListAsync(null, null, startDate, endDate, 0, int.MaxValue);

            var userLogs = auditLogs.Where(a => a.UserName == userName);

            return new
            {
                UserName = userName,
                Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                TotalActions = userLogs.Count(),
                ActionsByType = userLogs.GroupBy(a => a.ActionType)
                    .Select(g => new { ActionType = g.Key, Count = g.Count() }),
                TableActivity = userLogs.GroupBy(a => a.TableName)
                    .Select(g => new { TableName = g.Key, Count = g.Count() }),
                DailyActivity = userLogs.GroupBy(a => a.Timestamp.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
            };
        }

        public async Task<object> GetSystemActivitySummaryAsync(DateTime startDate, DateTime endDate)
        {
            var auditLogs = await _auditRepo.ListAsync(null, null, startDate, endDate, 0, int.MaxValue);

            return new
            {
                Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                TotalActions = auditLogs.Count(),
                UniqueUsers = auditLogs.Select(a => a.UserName).Distinct().Count(),
                ActionsByType = auditLogs.GroupBy(a => a.ActionType)
                    .Select(g => new { ActionType = g.Key, Count = g.Count() }),
                MostActiveUsers = auditLogs.GroupBy(a => a.UserName)
                    .Select(g => new { UserName = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(10),
                MostModifiedTables = auditLogs.GroupBy(a => a.TableName)
                    .Select(g => new { TableName = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(10)
            };
        }
    }
}