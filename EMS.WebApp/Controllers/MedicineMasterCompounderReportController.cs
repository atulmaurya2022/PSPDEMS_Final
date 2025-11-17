using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize(Policy = "AccessMedicineMasterCompounderReport")]
    public class MedicineMasterCompounderReportController : Controller
    {
        private readonly ICompounderIndentRepository _repo;

        public MedicineMasterCompounderReportController(ICompounderIndentRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index()
        {
            return View("MedicineMasterCompounderReport");
        }

        [HttpGet]
        public async Task<IActionResult> GetReport()
        {
            try
            {
                // Get current user's plant information
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                // Get plant details for display
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var plantInfo = await dbContext.org_plants
                    .Where(p => p.plant_id == userPlantId)
                    .Select(p => new { p.plant_name, p.plant_code })
                    .FirstOrDefaultAsync();

                // Pass plant filtering to repository
                var reportData = await _repo.GetMedicineMasterCompounderReportAsync(userPlantId);

                var result = new
                {
                    success = true,
                    data = reportData.Select((item, index) => new
                    {
                        slNo = index + 1,
                        medName = item.MedName,
                        totalQtyInStore = item.TotalQtyInStore,
                        expiredQty = item.ExpiredQty,
                        reorderLimit = item.ReorderLimit,
                        needsReorder = item.TotalQtyInStore <= item.ReorderLimit,
                        stockStatus = GetStockStatus(item.TotalQtyInStore, item.ExpiredQty, item.ReorderLimit)
                    }),
                    reportInfo = new
                    {
                        title = "MEDICINE MASTER COMPOUNDER REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A", // NEW: Dynamic plant code
                        plantName = plantInfo?.plant_name ?? "Unknown Plant", // NEW: Dynamic plant name
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count(),
                        totalQtyInStore = reportData.Sum(r => r.TotalQtyInStore),
                        totalExpiredQty = reportData.Sum(r => r.ExpiredQty),
                        medicinesNeedingReorder = reportData.Count(r => r.TotalQtyInStore <= r.ReorderLimit),
                        medicinesWithExpired = reportData.Count(r => r.ExpiredQty > 0)
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while generating the medicine master report." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Export()
        {
            try
            {
                // Get current user's plant information
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                var reportData = await _repo.GetMedicineMasterCompounderReportAsync(userPlantId);

                // CSV format
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("MEDICINE NAME,TOTAL QTY IN STORE,EXPIRED QTY,REORDER LIMIT,STOCK STATUS");

                foreach (var item in reportData)
                {
                    var stockStatus = GetStockStatus(item.TotalQtyInStore, item.ExpiredQty, item.ReorderLimit);
                    csv.AppendLine($"{item.MedName},{item.TotalQtyInStore},{item.ExpiredQty},{item.ReorderLimit},{stockStatus}");
                }

                var fileName = $"MedicineMasterCompounderReport_{DateTime.Now:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while exporting the report." });
            }
        }

        private string GetStockStatus(int totalQty, int reorderLimit, int expiredQty)
        {
            var availableQty = totalQty - expiredQty;

            if (totalQty == 0)
                return "Out of Stock";
            else if (availableQty == 0 && expiredQty > 0)
                return "All Stock Expired";
            else if (availableQty <= reorderLimit && expiredQty > 0)
                return "Critical - Low Available & Expired";
            else if (availableQty <= reorderLimit)
                return "Reorder Required";
            else if (expiredQty > 0 && availableQty > reorderLimit * 2) // If available is more than 2x reorder limit
                return "Good Stock";
            else if (expiredQty > 0 && availableQty > reorderLimit) // If available is above reorder limit but not 2x
                return "Adequate Stock";
            else if (expiredQty > 0)
                return "Expired Stock";
            else if (totalQty <= reorderLimit * 1.5) // 50% above reorder limit
                return "Low Stock";
            else
                return "Good Stock";
        }
    }
}