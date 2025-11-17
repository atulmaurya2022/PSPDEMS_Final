using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessStoreInventoryReport")]
    public class StoreInventoryReportController : Controller
    {
        private readonly IStoreIndentRepository _repo;

        public StoreInventoryReportController(IStoreIndentRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Display Store Inventory Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult Index()
        {
            return View("StoreInventoryReport");
        }

        /// <summary>
        /// Generate Store Inventory Batch Report
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Store Inventory Batch Report data</returns>

        [HttpGet]
        public async Task<IActionResult> GetReport(DateTime? fromDate = null, DateTime? toDate = null)
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

                // Set default date range if not provided (current month)
                if (!fromDate.HasValue)
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (!toDate.HasValue)
                    toDate = DateTime.Now.Date;

                // Pass plant filtering to repository
                var reportData = await _repo.GetStoreInventoryBatchReportAsync(fromDate, toDate, userPlantId);

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
                        potency = item.Potency,
                        manufacturerBy = item.ManufacturerBy,
                        batchNo = item.BatchNo,
                        vendorCode = item.VendorCode,
                        receivedQuantity = item.ReceivedQuantity,
                        availableStock = item.AvailableStock,
                        consumedStock = item.ConsumedStock,
                        expiryDate = item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set",
                        raisedBy = item.RaisedBy,
                        stockStatus = item.StockStatus
                    }),
                    reportInfo = new
                    {
                        title = "STORE INVENTORY - BATCH LEVEL REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A", // NEW: Dynamic plant code
                        plantName = plantInfo?.plant_name ?? "Unknown Plant", // NEW: Dynamic plant name
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count(),
                        totalBatches = reportData.Count(r => r.BatchNo != "No Batch Info"),
                        totalReceived = reportData.Sum(r => r.ReceivedQuantity),
                        totalAvailable = reportData.Sum(r => r.AvailableStock),
                        totalConsumed = reportData.Sum(r => r.ConsumedStock),
                        lowStockBatches = reportData.Count(r => r.StockStatus == "Low Stock"),
                        outOfStockBatches = reportData.Count(r => r.StockStatus == "Out of Stock")
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while generating the inventory report." });
            }
        }

        /// <summary>
        /// Export Store Inventory Batch Report to Excel
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Excel file</returns>
        [HttpGet]
        public async Task<IActionResult> Export(DateTime? fromDate = null, DateTime? toDate = null)
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

                // FIXED: Pass userPlantId for plant filtering
                var reportData = await _repo.GetStoreInventoryBatchReportAsync(fromDate, toDate, userPlantId);

                // CSV format for now
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("INDENT ID,RAISED DATE,MEDICINE NAME,RAISED QTY,POTENCY,MANUFACTURER BY,BATCH NO,VENDOR CODE,RECEIVED QTY,AVAILABLE STOCK,CONSUMED STOCK,EXPIRY DATE,RAISED BY,STOCK STATUS");

                foreach (var item in reportData)
                {
                    csv.AppendLine($"{item.IndentId},{item.RaisedDate:dd/MM/yyyy},{item.MedicineName},{item.RaisedQuantity},{item.Potency},{item.ManufacturerBy},{item.BatchNo},{item.VendorCode},{item.ReceivedQuantity},{item.AvailableStock},{item.ConsumedStock},{item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set"},{item.RaisedBy},{item.StockStatus}");
                }

                var fileName = $"StoreInventoryBatchReport_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while exporting the inventory report." });
            }
        }
    }
}