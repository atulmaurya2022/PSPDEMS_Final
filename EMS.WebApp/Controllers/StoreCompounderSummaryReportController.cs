using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    //[Authorize(Policy = "AccessStoreCompounderSummaryReport")]
    public class StoreCompounderSummaryReportController : Controller
    {
        private readonly IStoreIndentRepository _repo;

        public StoreCompounderSummaryReportController(IStoreIndentRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Display Store Compounder Summary Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult Index()
        {
            return View("StoreCompounderSummaryReport");
        }

        /// <summary>
        /// Generate Store Compounder Summary Report with plant filtering
        /// Shows medicine-wise distribution to each compounder in pivot format
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Store Compounder Summary Report data with dynamic columns</returns>
        [HttpGet]
        public async Task<IActionResult> StoreCompounderSummaryReport(DateTime? fromDate = null, DateTime? toDate = null)
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

                // Set default date range if not provided (current month)
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                // Get report data with pivot structure
                var reportResponse = await _repo.GetStoreCompounderSummaryReportAsync(
                    fromDate,
                    toDate,
                    userPlantId);

                // Transform data for JSON response
                var transformedData = reportResponse.Data.Select((item, index) => new
                {
                    slNo = index + 1,
                    medItemId = item.MedItemId,
                    medicineName = item.MedicineName,
                    totalStoreStock = item.TotalStoreStock,
                    compounderQuantities = item.CompounderQuantities,
                    totalIssuedToCompounders = item.TotalIssuedToCompounders,
                    remainingStock = item.RemainingStock
                });

                var result = new
                {
                    success = true,
                    data = transformedData,
                    compounderNames = reportResponse.CompounderNames,
                    totals = new
                    {
                        totalStoreStockSum = reportResponse.TotalStoreStockSum,
                        totalIssuedSum = reportResponse.TotalIssuedSum,
                        totalRemainingSum = reportResponse.TotalRemainingSum,
                        compounderTotals = reportResponse.CompounderTotals
                    },
                    reportInfo = new
                    {
                        title = "STORE COMPOUNDER SUMMARY",
                        plantCode = plantInfo?.plant_code ?? "N/A",
                        plantName = plantInfo?.plant_name ?? "Unknown Plant",
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalMedicines = reportResponse.Data.Count,
                        totalCompounders = reportResponse.CompounderNames.Count
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while generating the report." });
            }
        }

        /// <summary>
        /// Export Store Compounder Summary Report to CSV with dynamic compounder columns
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>CSV file</returns>
        [HttpGet]
        public async Task<IActionResult> ExportStoreCompounderSummaryReport(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Get current user's plant information
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                // Set default date range if not provided
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                // Get report data
                var reportResponse = await _repo.GetStoreCompounderSummaryReportAsync(
                    fromDate,
                    toDate,
                    userPlantId);

                // Build CSV with dynamic columns
                var csv = new System.Text.StringBuilder();

                // Build header row with dynamic compounder columns
                var headerParts = new List<string> { "MEDICINE NAME", "TOTAL STORE STOCK" };
                headerParts.AddRange(reportResponse.CompounderNames.Select(c => c.ToUpper()));
                headerParts.Add("REMAINING STOCK");
                csv.AppendLine(string.Join(",", headerParts));

                // Build data rows
                foreach (var item in reportResponse.Data)
                {
                    var rowParts = new List<string>
                    {
                        EscapeCsvField(item.MedicineName),
                        item.TotalStoreStock.ToString()
                    };

                    // Add compounder quantities in the same order as headers
                    foreach (var compounder in reportResponse.CompounderNames)
                    {
                        var qty = item.CompounderQuantities.ContainsKey(compounder)
                            ? item.CompounderQuantities[compounder]
                            : 0;
                        rowParts.Add(qty.ToString());
                    }

                    rowParts.Add(item.RemainingStock.ToString());
                    csv.AppendLine(string.Join(",", rowParts));
                }

                // Add totals row
                var totalRowParts = new List<string>
                {
                    "TOTAL",
                    reportResponse.TotalStoreStockSum.ToString()
                };

                foreach (var compounder in reportResponse.CompounderNames)
                {
                    var total = reportResponse.CompounderTotals.ContainsKey(compounder)
                        ? reportResponse.CompounderTotals[compounder]
                        : 0;
                    totalRowParts.Add(total.ToString());
                }

                totalRowParts.Add(reportResponse.TotalRemainingSum.ToString());
                csv.AppendLine(string.Join(",", totalRowParts));

                var fileName = $"StoreCompounderSummaryReport_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while exporting the report." });
            }
        }

        /// <summary>
        /// Escape CSV field to handle commas and quotes
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}