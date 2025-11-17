using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessAuditLog")] // Add this policy to your authorization setup
    public class AuditLogController : Controller
    {
        private readonly IAuditLogRepository _auditRepo;

        public AuditLogController(IAuditLogRepository auditRepo)
        {
            _auditRepo = auditRepo;
        }

        public async Task<IActionResult> Index()
        {
            // Populate filter dropdowns
            ViewBag.TableNames = new SelectList(await _auditRepo.GetDistinctTableNamesAsync());
            ViewBag.ActionTypes = new SelectList(await _auditRepo.GetDistinctActionTypesAsync());

            return View();
        }

        public async Task<IActionResult> LoadData(string? tableName = null, string? actionType = null,
            DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 100)
        {
            try
            {
                var auditLogs = await _auditRepo.ListAsync(tableName, actionType, startDate, endDate, skip, take);
                var totalCount = await _auditRepo.CountAsync(tableName, actionType, startDate, endDate);

                var result = auditLogs.Select(a => new
                {
                    a.AuditId,
                    a.TableName,
                    a.ActionType,
                    a.RecordId,
                    a.UserName,
                    a.IpAddress,
                    a.Timestamp,
                    a.ControllerAction,
                    HasOldValues = !string.IsNullOrEmpty(a.OldValues),
                    HasNewValues = !string.IsNullOrEmpty(a.NewValues)
                });

                return Json(new { data = result, recordsTotal = totalCount, recordsFiltered = totalCount });
            }
            catch (Exception)
            {
                return Json(new { data = new List<object>(), recordsTotal = 0, recordsFiltered = 0, error = "Error loading audit data." });
            }
        }

        public async Task<IActionResult> Details(long id)
        {
            try
            {
                var auditLog = await _auditRepo.GetByIdAsync(id);
                if (auditLog == null) return NotFound();

                return PartialView("_Details", auditLog);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        public async Task<IActionResult> EntityHistory(string tableName, string recordId)
        {
            try
            {
                var history = await _auditRepo.GetEntityHistoryAsync(tableName, recordId);
                ViewBag.TableName = tableName;
                ViewBag.RecordId = recordId;

                return PartialView("_EntityHistory", history);
            }
            catch (Exception)
            {
                return PartialView("_EntityHistory", new List<SysAuditLog>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? tableName = null, string? actionType = null,
            DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var auditLogs = await _auditRepo.ListAsync(tableName, actionType, startDate, endDate, 0, 10000); // Max 10k records for export

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("AuditId,TableName,ActionType,RecordId,UserName,IpAddress,Timestamp,ControllerAction");

                foreach (var log in auditLogs)
                {
                    csv.AppendLine($"{log.AuditId},{log.TableName},{log.ActionType},{log.RecordId},{log.UserName},{log.IpAddress},{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.ControllerAction}");
                }

                var fileName = $"AuditLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting audit data.";
                return RedirectToAction("Index");
            }
        }
    }
}