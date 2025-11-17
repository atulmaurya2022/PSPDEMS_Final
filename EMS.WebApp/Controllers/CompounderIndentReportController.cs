using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize(Policy = "AccessCompounderIndentReport")]
    public class CompounderIndentReportController : Controller
    {
        private readonly ICompounderIndentRepository _repo;

        public CompounderIndentReportController(ICompounderIndentRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Display Compounder Indent Report View
        /// </summary>
        /// <returns>Report view</returns>
        public IActionResult Index()
        {
            return View("CompounderIndentReport");
        }

        /// <summary>
        /// Generate Compounder Indent Report with plant filtering
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Compounder Indent Report data</returns>
        [HttpGet]
        public async Task<IActionResult> CompounderIndentReport(DateTime? fromDate = null, DateTime? toDate = null)
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
                var reportData = await _repo.GetCompounderIndentReportAsync(fromDate, toDate, userPlantId);

                var result = new
                {
                    success = true,
                    data = reportData.Select((item, index) => new
                    {
                        slNo = index + 1,
                        indentId = item.IndentId,
                        indentDate = item.IndentDate.ToString("dd/MM/yyyy"),
                        medicineName = item.MedicineName,
                        manufacturerName = item.ManufacturerName,
                        quantity = item.Quantity,
                        raisedBy = item.RaisedBy
                    }),
                    reportInfo = new
                    {
                        title = "COMPOUNDER INDENT",
                        plantCode = plantInfo?.plant_code ?? "N/A", // Dynamic plant code
                        plantName = plantInfo?.plant_name ?? "Unknown Plant", // Dynamic plant name
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "System",
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = reportData.Count(),
                        totalQuantity = reportData.Sum(r => r.Quantity)
                    }
                };

                return Json(result);
            }
            catch (Exception )
            {
                return Json(new { success = false, message = "An error occurred while generating the report." });
            }
        }
        /// <summary>
        /// Export Compounder Indent Report to Excel with plant filtering and batch details
        /// </summary>
        /// <param name="fromDate">Start date for filtering</param>
        /// <param name="toDate">End date for filtering</param>
        /// <returns>Excel file</returns>
        [HttpGet]
        public async Task<IActionResult> ExportCompounderIndentReport(DateTime? fromDate = null, DateTime? toDate = null)
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

                var reportData = await _repo.GetCompounderIndentReportAsync(fromDate, toDate, userPlantId);

                // CSV format with only the 6 required columns
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("INDENT ID,INDENT DATE,MEDICINE NAME,MANUFACTURER NAME,QUANTITY,RAISED BY");

                foreach (var item in reportData)
                {
                    csv.AppendLine($"{item.IndentId},{item.IndentDate:dd/MM/yyyy},{item.MedicineName},{item.ManufacturerName},{item.Quantity},{item.RaisedBy}");
                }

                var fileName = $"CompounderIndentReport_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while exporting the report." });
            }
        }
    }
}
