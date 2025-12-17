using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize(Policy = "AccessCompounderInventoryReport")]
    public class CompounderInventoryReportController : Controller
    {
        private readonly ICompounderIndentRepository _repo;

        public CompounderInventoryReportController(ICompounderIndentRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Display Compounder Inventory Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult Index()
        {
            return View("CompounderInventoryReport");
        }

        /// <summary>
        /// Generate Compounder Inventory Report with plant filtering and batch details
        /// BCM plant: Compounders see only their own records, Doctors see all records
        /// Other plants: All users see all records
        /// </summary>
        /// <param name="fromDate">Start date for filtering (optional)</param>
        /// <param name="toDate">End date for filtering (optional)</param>
        /// <param name="showOnlyAvailable">Filter to show only items with available stock</param>
        /// <returns>Compounder Inventory Report data with batch details</returns>
        [HttpGet]
        public async Task<IActionResult> CompounderInventoryReport(DateTime? fromDate = null, DateTime? toDate = null, bool showOnlyAvailable = false)
        {
            try
            {
                // Get current user's plant information
                var currentUserName = User.Identity?.Name + " - " + User.GetFullName();
                var userPlantId = await _repo.GetUserPlantIdAsync(User.Identity?.Name);

                // Check if user is a Doctor (for BCM plant-specific access control)
                var isDoctor = User.IsInRole("Doctor");

                // Get plant details for display
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var plantInfo = await dbContext.org_plants
                    .Where(p => p.plant_id == userPlantId)
                    .Select(p => new { p.plant_name, p.plant_code })
                    .FirstOrDefaultAsync();

                // Pass plant filtering with BCM compounder-wise access control to repository
                var reportData = await _repo.GetCompounderInventoryReportAsync(
                    fromDate,
                    toDate,
                    userPlantId,
                    showOnlyAvailable,
                    currentUserName,  // NEW: Pass current user for BCM filtering
                    isDoctor);        // NEW: Pass isDoctor flag for BCM filtering

                var result = new
                {
                    success = true,
                    data = reportData.Select((item, index) => new
                    {
                        slNo = index + 1,
                        indentId = item.IndentId,
                        raisedDate = item.RaisedDate.ToString("dd/MM/yyyy"),
                        medicineName = item.MedicineName,
                        raisedQuantity = item.RaisedQuantity,
                        receivedQuantity = item.ReceivedQuantity,
                        potency = item.Potency,
                        manufacturerBy = item.ManufacturerBy,
                        batchNo = item.BatchNo,
                        vendorCode = item.VendorCode,
                        availableStock = item.AvailableStock,
                        consumedStock = item.ConsumedStock,
                        expiryDate = item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set",
                        raisedBy = item.RaisedBy,
                        stockStatus = item.StockStatus
                    }),
                    reportInfo = new
                    {
                        title = "COMPOUNDER INVENTORY - BATCH LEVEL REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A", // Dynamic plant code
                        plantName = plantInfo?.plant_name ?? "Unknown Plant", // Dynamic plant name
                        fromDate = fromDate?.ToString("dd/MM/yyyy") ?? "All Records",
                        toDate = toDate?.ToString("dd/MM/yyyy") ?? "All Records",
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count(),
                        totalBatches = reportData.Count(r => r.BatchNo != "No Batch Info"),
                        totalRaisedQuantity = reportData.Sum(r => r.RaisedQuantity),
                        totalReceivedQuantity = reportData.Sum(r => r.ReceivedQuantity),
                        totalAvailableStock = reportData.Sum(r => r.AvailableStock),
                        totalConsumedStock = reportData.Sum(r => r.ConsumedStock),
                        outOfStockItems = reportData.Count(r => r.StockStatus == "Out of Stock"),
                        lowStockItems = reportData.Count(r => r.StockStatus == "Low Stock")
                    }
                };

                return Json(result);
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while generating the inventory report." });
            }
        }

        /// <summary>
        /// Export Compounder Inventory Report to Excel with plant filtering and batch details
        /// BCM plant: Compounders see only their own records, Doctors see all records
        /// </summary>
        /// <param name="fromDate">Start date for filtering (optional)</param>
        /// <param name="toDate">End date for filtering (optional)</param>
        /// <param name="showOnlyAvailable">Filter to show only items with available stock</param>
        /// <returns>Excel file</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCompounderInventoryReport(DateTime? fromDate = null, DateTime? toDate = null, bool showOnlyAvailable = false)
        {
            try
            {
                // Get current user's plant information
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                // Check if user is a Doctor (for BCM plant-specific access control)
                var isDoctor = User.IsInRole("Doctor");

                // Pass plant filtering with BCM compounder-wise access control to repository
                var reportData = await _repo.GetCompounderInventoryReportAsync(
                    fromDate,
                    toDate,
                    userPlantId,
                    showOnlyAvailable,
                    currentUserName,  // NEW: Pass current user for BCM filtering
                    isDoctor);        // NEW: Pass isDoctor flag for BCM filtering

                // CSV format with batch details
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("INDENT ID,RAISED DATE,MEDICINE NAME,RAISED QTY,RECEIVED QTY,POTENCY,MANUFACTURER,BATCH NO,VENDOR CODE,AVAILABLE STOCK,CONSUMED STOCK,EXPIRY DATE,RAISED BY,STOCK STATUS");

                foreach (var item in reportData)
                {
                    csv.AppendLine($"{item.IndentId},{item.RaisedDate:dd/MM/yyyy},{item.MedicineName},{item.RaisedQuantity},{item.ReceivedQuantity},{item.Potency},{item.ManufacturerBy},{item.BatchNo},{item.VendorCode},{item.AvailableStock},{item.ConsumedStock},{item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set"},{item.RaisedBy},{item.StockStatus}");
                }

                var dateRangeText = fromDate.HasValue && toDate.HasValue
                    ? $"{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}"
                    : "AllRecords";
                var fileName = $"CompounderInventoryBatchReport_{dateRangeText}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "An error occurred while exporting the inventory report." });
            }
        }
    }
}