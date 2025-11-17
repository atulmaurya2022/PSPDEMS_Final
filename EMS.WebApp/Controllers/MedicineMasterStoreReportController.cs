using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedicineMasterStoreReport")] // Using same authorization as store indent
    public class MedicineMasterStoreReportController : Controller
    {
        private readonly IStoreIndentRepository _repo;

        public MedicineMasterStoreReportController(IStoreIndentRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Display Medicine Master Store Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult Index()
        {
            return View("MedicineMasterStoreReport");
        }

        /// <summary>
        /// Generate Medicine Master Store Report
        /// </summary>
        /// <returns>Medicine Master Store Report data</returns>
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
                var reportData = await _repo.GetMedicineMasterStoreReportAsync(userPlantId);

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
                        stockStatus = GetStockStatus(item.TotalQtyInStore, item.ReorderLimit, item.ExpiredQty),
                        availableQty = item.TotalQtyInStore - item.ExpiredQty
                    }),
                    reportInfo = new
                    {
                        title = "MEDICINE MASTER STORE REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A", // NEW: Dynamic plant code
                        plantName = plantInfo?.plant_name ?? "Unknown Plant", // NEW: Dynamic plant name
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName(),
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count(),
                        totalQtyInStore = reportData.Sum(r => r.TotalQtyInStore),
                        totalExpiredQty = reportData.Sum(r => r.ExpiredQty),
                        totalAvailableQty = reportData.Sum(r => r.TotalQtyInStore - r.ExpiredQty),
                        medicinesNeedingReorder = reportData.Count(r => r.TotalQtyInStore <= r.ReorderLimit),
                        medicinesWithExpired = reportData.Count(r => r.ExpiredQty > 0),
                        medicinesOutOfStock = reportData.Count(r => r.TotalQtyInStore == 0)
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MedicineMasterStoreReport Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while generating the report." });
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
        /// <summary>
        /// Export Medicine Master Store Report to Excel
        /// </summary>
        /// <returns>Excel file</returns>
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            try
            {
                // Get current user's plant information (FIXED: Add plant-wise access)
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                // Get plant details for display
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var plantInfo = await dbContext.org_plants
                    .Where(p => p.plant_id == userPlantId)
                    .Select(p => new { p.plant_name, p.plant_code })
                    .FirstOrDefaultAsync();

                // FIXED: Pass plant filtering to repository
                var reportData = await _repo.GetMedicineMasterStoreReportAsync(userPlantId);

                // Check if data exists
                if (reportData == null)
                {
                    return Json(new { success = false, message = "No data found." });
                }

                // Set EPPlus license context (required for EPPlus 5.0+)
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var fileBytes = GenerateExcelFile(reportData, plantInfo?.plant_name, plantInfo?.plant_code);
                var fileName = $"MedicineMasterStoreReport_{plantInfo?.plant_code ?? "Plant"}_{DateTime.Now:ddMMyyyy_HHmm}.xlsx";

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                // Log the detailed error
                System.Diagnostics.Debug.WriteLine($"ExportMedicineMasterStoreReport Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                // Return detailed error for debugging (remove in production)
                return Json(new { success = false, message = $"Export error: {ex.Message}" });
            }
        }

        private byte[] GenerateExcelFile(IEnumerable<MedicineMasterStoreReportDto> reportData, string plantName = null, string plantCode = null)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Medicine Master Store Report");

            try
            {
                // Add report header
                worksheet.Cells[1, 1].Value = "MEDICINE MASTER STORE REPORT";
                worksheet.Cells[1, 1, 1, 7].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Add plant information (FIXED: Include plant details)
                worksheet.Cells[2, 1].Value = $"Plant: {plantCode ?? "N/A"} - {plantName ?? "Unknown Plant"}";
                worksheet.Cells[2, 1, 2, 7].Merge = true;
                worksheet.Cells[2, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[2, 1].Style.Font.Size = 12;
                worksheet.Cells[2, 1].Style.Font.Bold = true;

                // Add generated info
                worksheet.Cells[3, 1].Value = $"Generated by: {User.Identity?.Name ?? "System"} - {User.GetFullName()} | Generated on: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Cells[3, 1, 3, 7].Merge = true;
                worksheet.Cells[3, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[3, 1].Style.Font.Size = 10;

                // Add empty row
                var headerRow = 5;

                // Add column headers
                var headers = new[]
                {
            "SL NO",
            "MEDICINE NAME",
            "TOTAL QTY IN STORE",
            "EXPIRED QTY",
            "REORDER LIMIT",
            "AVAILABLE QTY",
            "STOCK STATUS"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[headerRow, i + 1].Value = headers[i];
                    worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    worksheet.Cells[headerRow, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Add data rows
                var dataStartRow = headerRow + 1;
                var currentRow = dataStartRow;

                var dataList = reportData.ToList();
                for (int i = 0; i < dataList.Count; i++)
                {
                    var item = dataList[i];
                    var availableQty = item.TotalQtyInStore - item.ExpiredQty;

                    worksheet.Cells[currentRow, 1].Value = i + 1;
                    worksheet.Cells[currentRow, 2].Value = item.MedName;
                    worksheet.Cells[currentRow, 3].Value = item.TotalQtyInStore;
                    worksheet.Cells[currentRow, 4].Value = item.ExpiredQty;
                    worksheet.Cells[currentRow, 5].Value = item.ReorderLimit;
                    worksheet.Cells[currentRow, 6].Value = availableQty;

                    // Determine stock status and apply formatting
                    var stockStatus = GetStockStatus(item.TotalQtyInStore, item.ReorderLimit, item.ExpiredQty);
                    worksheet.Cells[currentRow, 7].Value = stockStatus;

                    // Center align quantity columns
                    for (int col = 3; col <= 6; col++)
                    {
                        worksheet.Cells[currentRow, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    // Apply conditional formatting
                    if (item.ExpiredQty > 0)
                    {
                        worksheet.Cells[currentRow, 4].Style.Font.Color.SetColor(Color.Red);
                        worksheet.Cells[currentRow, 4].Style.Font.Bold = true;
                    }

                    if (item.TotalQtyInStore <= item.ReorderLimit)
                    {
                        worksheet.Cells[currentRow, 3].Style.Font.Color.SetColor(Color.Orange);
                        worksheet.Cells[currentRow, 3].Style.Font.Bold = true;
                    }

                    if (item.TotalQtyInStore == 0)
                    {
                        worksheet.Cells[currentRow, 3].Style.Font.Color.SetColor(Color.Red);
                        worksheet.Cells[currentRow, 3].Style.Font.Bold = true;
                        worksheet.Cells[currentRow, 7].Style.Font.Color.SetColor(Color.Red);
                        worksheet.Cells[currentRow, 7].Style.Font.Bold = true;
                    }
                    else if (availableQty <= item.ReorderLimit)
                    {
                        worksheet.Cells[currentRow, 6].Style.Font.Color.SetColor(Color.Orange);
                        worksheet.Cells[currentRow, 6].Style.Font.Bold = true;
                        worksheet.Cells[currentRow, 7].Style.Font.Color.SetColor(Color.Red);
                        worksheet.Cells[currentRow, 7].Style.Font.Bold = true;
                    }

                    currentRow++;
                }

                // Add totals row
                if (dataList.Any())
                {
                    var totalsRow = currentRow + 1;
                    worksheet.Cells[totalsRow, 1].Value = "TOTALS:";
                    worksheet.Cells[totalsRow, 1, totalsRow, 2].Merge = true;
                    worksheet.Cells[totalsRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[totalsRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    worksheet.Cells[totalsRow, 3].Value = dataList.Sum(r => r.TotalQtyInStore);
                    worksheet.Cells[totalsRow, 3].Style.Font.Bold = true;
                    worksheet.Cells[totalsRow, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.Cells[totalsRow, 4].Value = dataList.Sum(r => r.ExpiredQty);
                    worksheet.Cells[totalsRow, 4].Style.Font.Bold = true;
                    worksheet.Cells[totalsRow, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.Cells[totalsRow, 6].Value = dataList.Sum(r => r.TotalQtyInStore - r.ExpiredQty);
                    worksheet.Cells[totalsRow, 6].Style.Font.Bold = true;
                    worksheet.Cells[totalsRow, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    if (dataList.Sum(r => r.ExpiredQty) > 0)
                    {
                        worksheet.Cells[totalsRow, 4].Style.Font.Color.SetColor(Color.Red);
                    }

                    // Add summary info
                    var summaryRow = totalsRow + 3;
                    worksheet.Cells[summaryRow, 1].Value = "SUMMARY";
                    worksheet.Cells[summaryRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[summaryRow, 1].Style.Font.Size = 12;

                    worksheet.Cells[summaryRow + 1, 1].Value = $"Total Medicines: {dataList.Count}";
                    worksheet.Cells[summaryRow + 2, 1].Value = $"Medicines Needing Reorder: {dataList.Count(r => r.TotalQtyInStore <= r.ReorderLimit)}";
                    worksheet.Cells[summaryRow + 3, 1].Value = $"Medicines with Expired Stock: {dataList.Count(r => r.ExpiredQty > 0)}";
                    worksheet.Cells[summaryRow + 4, 1].Value = $"Medicines Out of Stock: {dataList.Count(r => r.TotalQtyInStore == 0)}";
                }

                // Add borders to all data cells
                var totalRows = currentRow - 1;
                if (totalRows >= headerRow)
                {
                    var dataRange = worksheet.Cells[headerRow, 1, totalRows, headers.Length];
                    dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                    dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                return package.GetAsByteArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateExcelFile Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fallback CSV Export with plant-wise access control
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportCSV()
        {
            try
            {
                // FIXED: Get current user's plant information
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                // Get plant details for filename
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var plantInfo = await dbContext.org_plants
                    .Where(p => p.plant_id == userPlantId)
                    .Select(p => new { p.plant_name, p.plant_code })
                    .FirstOrDefaultAsync();

                // FIXED: Pass plant filtering to repository
                var reportData = await _repo.GetMedicineMasterStoreReportAsync(userPlantId);

                var csv = new System.Text.StringBuilder();

                // Add header with plant information
                csv.AppendLine($"MEDICINE MASTER STORE REPORT");
                csv.AppendLine($"Plant: {plantInfo?.plant_code ?? "N/A"} - {plantInfo?.plant_name ?? "Unknown Plant"}");
                csv.AppendLine($"Generated by: {User.Identity?.Name ?? "System"} - {User.GetFullName()}");
                csv.AppendLine($"Generated on: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                csv.AppendLine(); // Empty line

                // Add column headers
                csv.AppendLine("SL NO,MEDICINE NAME,TOTAL QTY IN STORE,EXPIRED QTY,REORDER LIMIT,AVAILABLE QTY,STOCK STATUS");

                var dataList = reportData.ToList();
                for (int i = 0; i < dataList.Count; i++)
                {
                    var item = dataList[i];
                    var availableQty = item.TotalQtyInStore - item.ExpiredQty;
                    var stockStatus = GetStockStatus(item.TotalQtyInStore, item.ReorderLimit, item.ExpiredQty);

                    csv.AppendLine($"{i + 1},\"{item.MedName}\",{item.TotalQtyInStore},{item.ExpiredQty},{item.ReorderLimit},{availableQty},\"{stockStatus}\"");
                }

                // Add summary
                csv.AppendLine(); // Empty line
                csv.AppendLine("SUMMARY");
                csv.AppendLine($"Total Medicines: {dataList.Count}");
                csv.AppendLine($"Total Quantity in Store: {dataList.Sum(r => r.TotalQtyInStore)}");
                csv.AppendLine($"Total Expired Quantity: {dataList.Sum(r => r.ExpiredQty)}");
                csv.AppendLine($"Total Available Quantity: {dataList.Sum(r => r.TotalQtyInStore - r.ExpiredQty)}");
                csv.AppendLine($"Medicines Needing Reorder: {dataList.Count(r => r.TotalQtyInStore <= r.ReorderLimit)}");
                csv.AppendLine($"Medicines with Expired Stock: {dataList.Count(r => r.ExpiredQty > 0)}");
                csv.AppendLine($"Medicines Out of Stock: {dataList.Count(r => r.TotalQtyInStore == 0)}");

                var fileName = $"MedicineMasterStoreReport_{plantInfo?.plant_code ?? "Plant"}_{DateTime.Now:ddMMyyyy_HHmm}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportMedicineMasterStoreReportCSV Error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while exporting the CSV report." });
            }
        }
    }
}