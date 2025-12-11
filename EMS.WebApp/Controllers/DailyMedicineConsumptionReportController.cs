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
            var currentUserName = User.Identity?.Name + " - " + User.GetFullName();
            var userPlantId = await _repo.GetUserPlantIdAsync(User.Identity?.Name);

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

            // Check if current user is a doctor for BCM plant-specific filtering
            var userRole = await GetUserRoleAsync();
            bool isDoctor = userRole?.ToLower() == "doctor";
            // bool isDoctor = User.IsInRole("Doctor");

            // DEBUG: Log role information
            var userRoles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            Console.WriteLine($"👤 User: {currentUserName}");
            Console.WriteLine($"🔑 User Roles: [{string.Join(", ", userRoles)}]");
            Console.WriteLine($"👨‍⚕️ Is Doctor: {isDoctor}");
            Console.WriteLine($"🏭 Plant Code: {plantInfo?.plant_code}");

            // Pass date filtering and BCM parameters to repository
            var reportData = await _repo.GetDailyMedicineConsumptionReportAsync(
                effectiveFromDate,
                effectiveToDate,
                userPlantId,
                currentUserName,
                isDoctor
            );

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

            // Add compounder-wise indicator for BCM plant (only for non-doctors)
            string reportScope = "";
            bool isBcmPlant = plantInfo?.plant_code?.ToUpper() == "BCM";
            if (isBcmPlant && !isDoctor)
            {
                reportScope = $" (My Consumption Only - {User.GetFullName()})";
            }

            var result = new
            {
                success = true,
                data = reportData.Select((item, index) => new
                {
                    slNo = index + 1,
                    medicineName = item.MedicineName,
                    totalStockInCompounderInventory = item.TotalStockInCompounderInventory,
                    issuedQty = item.IssuedQty,
                    expiredQty = item.ExpiredQty,
                    // NEW: Added total available at compounder inventory
                    totalAvailableAtCompounderInventory = item.TotalAvailableAtCompounderInventory
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
                    // NEW: Added total available at compounder inventory sum
                    totalAvailableAtCompounderInventory = reportData.Sum(r => r.TotalAvailableAtCompounderInventory),
                    reportPeriod = reportPeriod + reportScope,
                    fromDate = effectiveFromDate.ToString("yyyy-MM-dd"),
                    toDate = effectiveToDate.ToString("yyyy-MM-dd"),
                    fromDateDisplay = effectiveFromDate.ToString("dd/MM/yyyy"),
                    toDateDisplay = effectiveToDate.ToString("dd/MM/yyyy"),
                    isToday = effectiveFromDate.Date == DateTime.Today && effectiveToDate.Date == DateTime.Today,
                    isSingleDay = effectiveFromDate.Date == effectiveToDate.Date,
                    // BCM plant-specific information
                    isBcmPlant = isBcmPlant,
                    isCompounderView = isBcmPlant && !isDoctor,
                    compounderName = !isDoctor ? User.GetFullName() : null,
                    // DEBUG info (can remove in production)
                    debugInfo = new
                    {
                        userRoles = string.Join(", ", userRoles),
                        isDoctorDetected = isDoctor
                    }
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

            // Get plant code for BCM check
            var plantCode = await _repo.GetPlantCodeByIdAsync(userPlantId ?? 0);
            var userRole = await GetUserRoleAsync();
            bool isDoctor = userRole?.ToLower() == "doctor";

            // Default to current date if no dates provided
            var effectiveFromDate = fromDate ?? DateTime.Today;
            var effectiveToDate = toDate ?? DateTime.Today;

            // Pass BCM parameters to repository
            var reportData = await _repo.GetDailyMedicineConsumptionReportAsync(
                effectiveFromDate,
                effectiveToDate,
                userPlantId,
                currentUserName,
                isDoctor
            );

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

            // Add compounder-specific indicator for BCM plant
            bool isBcmPlant = plantCode?.ToUpper() == "BCM";
            if (isBcmPlant && !isDoctor)
            {
                csv.AppendLine($"Compounder: {User.GetFullName()} (My Consumption Only)");
            }

            csv.AppendLine($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            csv.AppendLine($"Generated By: {User.Identity?.Name + " - " + User.GetFullName() ?? "System"}");
            csv.AppendLine();
            // UPDATED: Changed column header and added new column
            csv.AppendLine("MEDICINE NAME,TOTAL AVAILABLE AT COMPOUNDER STOCK,ISSUED QTY,EXPIRED QTY,TOTAL AVAILABLE AT COMPOUNDER INVENTORY");

            foreach (var item in reportData)
            {
                // UPDATED: Added new column value
                csv.AppendLine($"{item.MedicineName},{item.TotalStockInCompounderInventory},{item.IssuedQty},{item.ExpiredQty},{item.TotalAvailableAtCompounderInventory}");
            }

            // Generate filename
            string fileName;
            string compounderSuffix = (isBcmPlant && !isDoctor) ? $"_{currentUserName}" : "";

            if (effectiveFromDate.Date == effectiveToDate.Date)
            {
                fileName = $"MedicineConsumptionReport{compounderSuffix}_{effectiveFromDate:ddMMyyyy}.csv";
            }
            else
            {
                fileName = $"MedicineConsumptionReport{compounderSuffix}_{effectiveFromDate:ddMMyyyy}_to_{effectiveToDate:ddMMyyyy}.csv";
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

    private async Task<string?> GetUserRoleAsync()
    {
        try
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return null;

            using var scope = HttpContext.RequestServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = await dbContext.SysUsers
                .Include(u => u.SysRole)
                .FirstOrDefaultAsync(u => u.full_name == userName || u.email == userName || u.adid == userName);

            return user?.SysRole?.role_name;
        }
        catch (Exception ex)
        {

            return null;
        }
    }
}