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
    [Authorize("AccessStoreIndentReport")]
    public class StoreIndentReportController : Controller
    {
        private readonly IStoreIndentRepository _repo;

        public StoreIndentReportController(IStoreIndentRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Display Store Indent Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult Index()
        {
            return View("StoreIndentReport");
        }

        /// <summary>
        /// Generate Store Indent Batch Report
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Store Indent Batch Report data</returns>
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
                var reportData = await _repo.GetStoreIndentBatchReportAsync(fromDate, toDate, userPlantId);

                var result = new
                {
                    success = true,
                    data = reportData.Select((item, index) => new
                    {
                        slNo = index + 1,
                        indentId = item.IndentId,
                        indentDate = item.IndentDate.ToString("dd/MM/yyyy"),
                        medicineName = item.MedicineName,
                        potency = item.Potency,
                        manufacturerName = item.ManufacturerName,
                        batchNo = item.BatchNo,
                        vendorCode = item.VendorCode,
                        raisedQuantity = item.RaisedQuantity,
                        receivedQuantity = item.ReceivedQuantity,
                        expiryDate = item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set",
                        raisedBy = item.RaisedBy,
                        status = item.Status
                    }),
                    reportInfo = new
                    {
                        title = "STORE INDENT - BATCH LEVEL REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A", // NEW: Dynamic plant code
                        plantName = plantInfo?.plant_name ?? "Unknown Plant", // NEW: Dynamic plant name
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName(),
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count(),
                        totalBatches = reportData.Count(r => r.BatchNo != "No Batch Info"),
                        totalRaisedQuantity = reportData.Sum(r => r.RaisedQuantity),
                        totalReceivedQuantity = reportData.Sum(r => r.ReceivedQuantity)
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
        /// Export Store Indent Batch Report to Excel
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
                var reportData = await _repo.GetStoreIndentBatchReportAsync(fromDate, toDate, userPlantId);

                // CSV format for now
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("INDENT ID,INDENT DATE,MEDICINE NAME,POTENCY,MANUFACTURER NAME,BATCH NO,VENDOR CODE,RAISED QTY,RECEIVED QTY,EXPIRY DATE,RAISED BY,STATUS");

                foreach (var item in reportData)
                {
                    csv.AppendLine($"{item.IndentId},{item.IndentDate:dd/MM/yyyy},{item.MedicineName},{item.Potency},{item.ManufacturerName},{item.BatchNo},{item.VendorCode},{item.RaisedQuantity},{item.ReceivedQuantity},{item.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set"},{item.RaisedBy},{item.Status}");
                }

                var fileName = $"StoreIndentBatchReport_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while exporting the report." });
            }
        }
    }
}