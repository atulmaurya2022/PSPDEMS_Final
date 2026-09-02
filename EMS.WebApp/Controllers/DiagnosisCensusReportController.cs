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

        // ============================================================
        // ✅ BCM Role-based access helpers — mirror DiseaseTrendReportController.
        // ============================================================

        /// <summary>
        /// Returns "ADID - FullName" — exact format stored in MedPrescriptions.CreatedBy.
        /// Required for the CreatedBy filter to match anything.
        /// </summary>
        private string GetCurrentUserDisplay()
            => (User.Identity?.Name + " - " + User.GetFullName()).Trim(' ', '-');

        /// <summary>
        /// Reads role from SysUsers → SysRole.role_name. Mirrors DiseaseTrendReportController.GetUserRoleAsync.
        /// </summary>
        private async Task<string?> GetUserRoleAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName)) return null;

                var user = await _db.SysUsers
                    .Include(u => u.SysRole)
                    .FirstOrDefaultAsync(u => u.full_name == userName
                                           || u.email == userName
                                           || u.adid == userName);

                return user?.SysRole?.role_name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Doctor or Admin → see all records. Matches DiseaseTrendReportController.IsDocterOrAdmin.
        /// </summary>
        private static bool IsDocterOrAdmin(string? userRole)
        {
            if (string.IsNullOrEmpty(userRole)) return false;
            var role = userRole.ToLower();
            return role == "doctor" || role.Contains("admin");
        }

        [HttpGet]
        public async Task<IActionResult> GetReport(DateTime? fromDate = null, DateTime? toDate = null, short? deptId = null)
        {
            try
            {
                var currentUserName = User.Identity?.Name;
                var currentUserDisplay = GetCurrentUserDisplay();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!toDate.HasValue) toDate = DateTime.Now.Date;

                var counts = (await _service.GetDiagnosisCensusCountsAsync(
                    currentUserName, fromDate, toDate, deptId,
                    isDoctor, userRole, currentUserDisplay)).ToList();
                var diseases = await _service.GetAllDiseasesAsync(userPlantId);
                var departments = (await _service.GetDepartmentsAsync()).ToList();

                // ✅ Bottom-of-report breakdowns — Visitors / Manager / ESP / Children / Spouse
                var visitorCounts = (await _service.GetVisitorDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, deptId, isDoctor, userRole, currentUserDisplay)).ToList();
                var managerCounts = (await _service.GetManagerDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();
                var espCounts = (await _service.GetEspDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();
                var childCounts = (await _service.GetChildDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();
                var spouseCounts = (await _service.GetSpouseDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();

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
                    // ✅ extra bottom rows — kept separate from the department pivot's totals
                    extraRows = new object[]
                    {
                        new
                        {
                            label = "VISITORS",
                            isVisitorRow = false,
                            counts = visitorCounts.Select(c => new { diseaseId = c.DiseaseId, count = c.Count })
                        },
                        new
                        {
                            label = "MANAGER",
                            isVisitorRow = false,
                            counts = managerCounts.Select(c => new { diseaseId = c.DiseaseId, count = c.Count })
                        },
                        new
                        {
                            label = "ESP",
                            isVisitorRow = false,
                            counts = espCounts.Select(c => new { diseaseId = c.DiseaseId, count = c.Count })
                        },
                        new
                        {
                            label = "CHILDREN",
                            isVisitorRow = false,
                            counts = childCounts.Select(c => new { diseaseId = c.DiseaseId, count = c.Count })
                        },
                        new
                        {
                            label = "SPOUSE",
                            isVisitorRow = false,
                            counts = spouseCounts.Select(c => new { diseaseId = c.DiseaseId, count = c.Count })
                        }
                    },
                    reportInfo = new
                    {
                        title = "DIAGNOSIS CENSUS REPORT",
                        plantCode = plantInfo?.plant_code ?? "N/A",
                        plantName = plantInfo?.plant_name ?? "Unknown Plant",
                        fromDate = fromDate?.ToString("dd/MM/yyyy"),
                        toDate = toDate?.ToString("dd/MM/yyyy"),
                        generatedBy = User.Identity?.Name + " - " + User.GetFullName(),
                        generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                        totalRecords = counts.Sum(c => c.Count)
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
                var currentUserDisplay = GetCurrentUserDisplay();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);
                var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

                if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                if (!toDate.HasValue) toDate = DateTime.Now.Date;

                var counts = (await _service.GetDiagnosisCensusCountsAsync(
                    currentUserName, fromDate, toDate, deptId,
                    isDoctor, userRole, currentUserDisplay)).ToList();
                var diseases = (await _service.GetAllDiseasesAsync(userPlantId)).ToList();
                var departments = (await _service.GetDepartmentsAsync()).ToList();

                // Ensure standard Other Diagnosis categories (Manager, ESP) are always included as rows
                var standardCategories = new List<OrgDepartmentDto>
                {
                    new OrgDepartmentDto { DeptId = -101, DeptName = "Manager" },
                    new OrgDepartmentDto { DeptId = -102, DeptName = "ESP" }
                };

                // ✅ Bottom-of-report breakdowns — Visitors / Manager / ESP / Children / Spouse
                var visitorCounts = (await _service.GetVisitorDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, deptId, isDoctor, userRole, currentUserDisplay)).ToList();
                var managerCounts = (await _service.GetManagerDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();
                var espCounts = (await _service.GetEspDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();
                var childCounts = (await _service.GetChildDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();
                var spouseCounts = (await _service.GetSpouseDiseaseCountsAsync(
                    currentUserName, fromDate, toDate, isDoctor, userRole, currentUserDisplay)).ToList();

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

                // Totals row (FINAL) — employee department rows only; extra buckets below are kept separate
                var totalRow = new System.Collections.Generic.List<string> { "TOTAL" };
                for (int i = 0; i < diseases.Count; i++)
                    totalRow.Add(columnTotals[i].ToString());
                totalRow.Add(grandTotal.ToString());
                csv.AppendLine(string.Join(",", totalRow.Select(EscapeCsv)));

                // ✅ Visitors / Manager / ESP / Children / Spouse rows — same disease columns, own row totals,
                // deliberately excluded from the employee TOTAL row above.
                csv.AppendLine();
                var extraBuckets = new (string Label, System.Collections.Generic.List<DiagnosisCensusCountDto> Rows)[]
                {
                    ("VISITORS", visitorCounts),
                    ("MANAGER", managerCounts),
                    ("ESP", espCounts),
                    ("CHILDREN", childCounts),
                    ("SPOUSE", spouseCounts)
                };

                foreach (var bucket in extraBuckets)
                {
                    var cols = new System.Collections.Generic.List<string> { bucket.Label };
                    long rowTotal = 0;

                    for (int i = 0; i < diseases.Count; i++)
                    {
                        var dis = diseases[i];
                        var match = bucket.Rows.FirstOrDefault(c => c.DiseaseId == dis.DiseaseId);
                        var val = match?.Count ?? 0;
                        cols.Add(val.ToString());
                        rowTotal += val;
                    }

                    cols.Add(rowTotal.ToString());
                    csv.AppendLine(string.Join(",", cols.Select(EscapeCsv)));
                }

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