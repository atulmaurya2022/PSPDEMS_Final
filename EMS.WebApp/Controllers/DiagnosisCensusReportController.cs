// File: Controllers/DiagnosisCensusReportController.cs
using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    // Using generic Authorize to avoid missing named-policy issue.
    [Authorize]
    public class DiagnosisCensusReportController : Controller
    {
        private readonly IDiagnosisCensusReportService _service;
        private readonly Services.IStoreIndentRepository _repo;
        private readonly ApplicationDbContext _db;

        public DiagnosisCensusReportController(
            IDiagnosisCensusReportService service,
            Services.IStoreIndentRepository repo,
            ApplicationDbContext db)
        {
            _service = service;
            _repo = repo;
            _db = db;
        }

        public IActionResult Index()
        {
            // View name matches file provided below
            return View("DiagnosisCensusReport");
        }

        [HttpGet]
        public async Task<IActionResult> GetReport(DateTime? fromDate = null, DateTime? toDate = null, short? deptId = null)
        {
            try
            {
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!toDate.HasValue) toDate = DateTime.Now.Date;

                var counts = (await _service.GetDiagnosisCensusCountsAsync(currentUserName, fromDate, toDate, deptId)).ToList();
                var diseases = await _service.GetAllDiseasesAsync(userPlantId);
                var departments = (await _service.GetDepartmentsAsync()).ToList();



                var plantInfo = await _db.org_plants
                    .Where(p => p.plant_id == userPlantId)
                    .Select(p => new { p.plant_name, p.plant_code })
                    .FirstOrDefaultAsync();

                var result = new
                {
                    success = true,
                    data = counts.Select((item, index) => new
                    {
                        slNo = index + 1,
                        deptId = item.DeptId,
                        deptName = item.DeptName,
                        diseaseId = item.DiseaseId,
                        diseaseName = item.DiseaseName,
                        count = item.Count
                    }),
                    diseases = diseases.Select(d => new { d.DiseaseId, d.DiseaseName }),
                    departments = departments.Select(d => new { d.DeptId, d.DeptName }),
                    reportInfo = new
                    {
                        title = "DIAGNOSIS CENSUS REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A",
                        plantName = plantInfo?.plant_name ?? "Unknown Plant",
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName(),
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = counts.Count()
                    }
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error while generating Diagnosis Census report." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Export(DateTime? fromDate = null, DateTime? toDate = null, short? deptId = null)
        {
            try
            {
                var currentUserName = User.Identity?.Name;
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!toDate.HasValue) toDate = DateTime.Now.Date;

                var counts = (await _service.GetDiagnosisCensusCountsAsync(currentUserName, fromDate, toDate, deptId)).ToList();
                var diseases = (await _service.GetAllDiseasesAsync(userPlantId)).ToList();
                var departments = (await _service.GetDepartmentsAsync()).ToList();

                var plantInfo = await _db.org_plants
                    .Where(p => p.plant_id == userPlantId)
                    .Select(p => new { p.plant_name, p.plant_code })
                    .FirstOrDefaultAsync();

                var csv = new System.Text.StringBuilder();

                // Top meta rows
                csv.AppendLine($"REPORT: DIAGNOSIS CENSUS REPORT");
                csv.AppendLine($"Plant Code:,{EscapeCsv(plantInfo?.plant_code ?? "N/A")}");
                csv.AppendLine($"Plant Name:,{EscapeCsv(plantInfo?.plant_name ?? "Unknown Plant")}");
                csv.AppendLine($"From Date:,{fromDate:dd/MM/yyyy}");
                csv.AppendLine($"To Date:,{toDate:dd/MM/yyyy}");
                csv.AppendLine($"Generated By:,{EscapeCsv(User.Identity?.Name + " - " + User.GetFullName())}");
                csv.AppendLine($"Generated On:,{DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                csv.AppendLine(); // blank row

                // Header: DEPARTMENT, <disease1>, <disease2>, ..., TOTAL
                var header = new System.Collections.Generic.List<string> { "DEPARTMENT" };
                header.AddRange(diseases.Select(d => d.DiseaseName));
                header.Add("TOTAL"); // column totals
                csv.AppendLine(string.Join(",", header.Select(EscapeCsv)));

                // Prepare column totals
                var columnTotals = new long[diseases.Count];

                long grandTotal = 0;

                // Rows
                foreach (var dept in departments)
                {
                    if (deptId.HasValue && dept.DeptId != deptId.Value) continue;

                    var cols = new System.Collections.Generic.List<string> { dept.DeptName };
                    long rowTotal = 0;

                    for (int i = 0; i < diseases.Count; i++)
                    {
                        var dis = diseases[i];
                        var match = counts.FirstOrDefault(c => c.DeptId == dept.DeptId && c.DiseaseId == dis.DiseaseId);
                        var val = match?.Count ?? 0;
                        cols.Add(val.ToString());

                        columnTotals[i] += val;
                        rowTotal += val;
                    }

                    cols.Add(rowTotal.ToString());
                    grandTotal += rowTotal;

                    csv.AppendLine(string.Join(",", cols.Select(EscapeCsv)));
                }

                // Totals row (FINAL)
                var totalRow = new System.Collections.Generic.List<string> { "TOTAL" };
                for (int i = 0; i < diseases.Count; i++)
                    totalRow.Add(columnTotals[i].ToString());
                totalRow.Add(grandTotal.ToString());
                csv.AppendLine(string.Join(",", totalRow.Select(EscapeCsv)));

                var fileName = $"DiagnosisCensus_{fromDate:ddMMyyyy}_to_{toDate:ddMMyyyy}.csv";
                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error while exporting Diagnosis Census report." });
            }
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Contains(",") || input.Contains("\"") || input.Contains("\n"))
                return $"\"{input.Replace("\"", "\"\"")}\"";
            return input;
        }
    }
}
