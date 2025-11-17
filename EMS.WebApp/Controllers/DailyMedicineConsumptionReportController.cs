using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = "AccessDailyMedicineConsumptionReport")]
public class DailyMedicineConsumptionReportController : Controller
{
    private readonly ICompounderIndentRepository _repo;

    public DailyMedicineConsumptionReportController(ICompounderIndentRepository repo)
    {
        _repo = repo;
    }

    public IActionResult Index()
    {
        return View("DailyMedicineConsumptionReport");
    }

    [HttpGet]
    public async Task<IActionResult> GetReport(DateTime? fromDate, DateTime? toDate)
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

            // Default to current date if no dates provided
            var effectiveFromDate = fromDate ?? DateTime.Today;
            var effectiveToDate = toDate ?? DateTime.Today;

            // Pass date filtering to repository
            var reportData = await _repo.GetDailyMedicineConsumptionReportAsync(effectiveFromDate, effectiveToDate, userPlantId);

            // Determine report period description
            string reportPeriod;
            if (effectiveFromDate.Date == effectiveToDate.Date)
            {
                if (effectiveFromDate.Date == DateTime.Today)
                {
                    reportPeriod = "Today's Consumption";
                }
                else
                {
                    reportPeriod = $"Single Day - {effectiveFromDate:dd/MM/yyyy}";
                }
            }
            else
            {
                reportPeriod = $"Period: {effectiveFromDate:dd/MM/yyyy} to {effectiveToDate:dd/MM/yyyy}";
            }

            var result = new
            {
                success = true,
                data = reportData.Select((item, index) => new
                {
                    slNo = index + 1,
                    medicineName = item.MedicineName,
                    totalStockInCompounderInventory = item.TotalStockInCompounderInventory,
                    issuedQty = item.IssuedQty, // Now shows date-filtered consumption
                    expiredQty = item.ExpiredQty
                }),
                reportInfo = new
                {
                    title = "DAILY MEDICINE CONSUMPTION REPORT",
                    plantCode = plantInfo?.plant_code ?? "N/A",
                    plantName = plantInfo?.plant_name ?? "Unknown Plant",
                    generatedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "System",
                    generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    totalRecords = reportData.Count(),
                    totalStockInInventory = reportData.Sum(r => r.TotalStockInCompounderInventory),
                    totalIssuedQty = reportData.Sum(r => r.IssuedQty),
                    totalExpiredQty = reportData.Sum(r => r.ExpiredQty),
                    // NEW: Date range information
                    reportPeriod = reportPeriod,
                    fromDate = effectiveFromDate.ToString("yyyy-MM-dd"),
                    toDate = effectiveToDate.ToString("yyyy-MM-dd"),
                    fromDateDisplay = effectiveFromDate.ToString("dd/MM/yyyy"),
                    toDateDisplay = effectiveToDate.ToString("dd/MM/yyyy"),
                    isToday = effectiveFromDate.Date == DateTime.Today && effectiveToDate.Date == DateTime.Today,
                    isSingleDay = effectiveFromDate.Date == effectiveToDate.Date
                }
            };

            return Json(result);
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = "An error occurred while generating the consumption report.",
                error = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Export(DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            // Get current user's plant information
            var currentUserName = User.Identity?.Name;
            var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

            // Default to current date if no dates provided
            var effectiveFromDate = fromDate ?? DateTime.Today;
            var effectiveToDate = toDate ?? DateTime.Today;

            var reportData = await _repo.GetDailyMedicineConsumptionReportAsync(effectiveFromDate, effectiveToDate, userPlantId);

            // CSV format with date range in header
            var csv = new System.Text.StringBuilder();

            // Add header with date range
            if (effectiveFromDate.Date == effectiveToDate.Date)
            {
                csv.AppendLine($"DAILY MEDICINE CONSUMPTION REPORT - {effectiveFromDate:dd/MM/yyyy}");
            }
            else
            {
                csv.AppendLine($"MEDICINE CONSUMPTION REPORT - {effectiveFromDate:dd/MM/yyyy} to {effectiveToDate:dd/MM/yyyy}");
            }
            csv.AppendLine($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            csv.AppendLine($"Generated By: {User.Identity?.Name + " - " + User.GetFullName() ?? "System"}");
            csv.AppendLine(); // Empty line
            csv.AppendLine("MEDICINE NAME,TOTAL STOCK IN COMPOUNDER INVENTORY,ISSUED QTY,EXPIRED QTY");

            foreach (var item in reportData)
            {
                csv.AppendLine($"{item.MedicineName},{item.TotalStockInCompounderInventory},{item.IssuedQty},{item.ExpiredQty}");
            }

            // Generate filename with date range
            string fileName;
            if (effectiveFromDate.Date == effectiveToDate.Date)
            {
                fileName = $"MedicineConsumptionReport_{effectiveFromDate:ddMMyyyy}.csv";
            }
            else
            {
                fileName = $"MedicineConsumptionReport_{effectiveFromDate:ddMMyyyy}_to_{effectiveToDate:ddMMyyyy}.csv";
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            return Json(new
            {
                success = false,
                message = "An error occurred while exporting the report.",
                error = ex.Message
            });
        }
    }
    
}