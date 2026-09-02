// File: Services/Reports/DiagnosisCensusReportService.cs
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Services.Reports
{
    public class DiagnosisCensusReportService : IDiagnosisCensusReportService
    {
        private readonly ApplicationDbContext _db;
        private readonly Services.IStoreIndentRepository _repo;

        public DiagnosisCensusReportService(ApplicationDbContext db, Services.IStoreIndentRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        // Doctor Diagnosis (MedPrescriptions) + Other Diagnosis (OthersDiagnosis for non-Others categories)
        public async Task<IEnumerable<DiagnosisCensusCountDto>> GetDiagnosisCensusCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            short? departmentId = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null)
        {
            if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) toDate = DateTime.Now.Date;

            var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

            // ✅ BCM compounder-wise filter: Doctor/Admin/Store see all; others restricted to own CreatedBy.
            // CreatedBy in MedPrescriptions is stored as "ADID - FullName" — passed in via currentUserCreatedBy.
            var createdByFilter = !string.IsNullOrEmpty(currentUserCreatedBy) ? currentUserCreatedBy : currentUserName;
            var applyCreatedByFilter = !CanSeeAllRecords(isDoctor, userRole) && !string.IsNullOrEmpty(createdByFilter);

            // Doctor Diagnosis rows (Self/Employees) — Grouped strictly by employee departments
            var presRowsQuery = from pd in _db.MedPrescriptionDiseases
                            join p in _db.MedPrescriptions on pd.PrescriptionId equals p.PrescriptionId
                            join d in _db.MedDiseases on pd.DiseaseId equals d.DiseaseId
                            join emp in _db.HrEmployees on p.emp_uid equals emp.emp_uid
                            join dept in _db.org_departments on emp.dept_id equals dept.dept_id
                            where p.ApprovalStatus == "Approved"
                                  && (p.DependentName == null || p.DependentName == "Self")
                                  && (!applyCreatedByFilter || p.CreatedBy == createdByFilter || p.CreatedBy == currentUserName)
                                  && p.PlantId == (userPlantId ?? p.PlantId)
                                  && p.PrescriptionDate >= fromDate.Value.Date
                                  && p.PrescriptionDate < toDate.Value.Date.AddDays(1) // half-open safe-range
                                  && (departmentId == null || emp.dept_id == departmentId)
                            select new
                            {
                                dept.dept_id,
                                dept.dept_name,
                                pd.DiseaseId,
                                emp.emp_uid
                            };

            var presRows = await presRowsQuery.ToListAsync();

            // Group Doctor Diagnosis by department
            var list = presRows
                .GroupBy(r => new { r.dept_id, r.dept_name, r.DiseaseId })
                .Select(g => new DiagnosisCensusCountDto
                {
                    DeptId = g.Key.dept_id,
                    DeptName = g.Key.dept_name,
                    DiseaseId = g.Key.DiseaseId,
                    DiseaseName = "",
                    Count = g.LongCount()
                })
                .ToList();

            // Map disease names
            var diseaseMap = await _db.MedDiseases
                                      .Select(d => new { d.DiseaseId, d.DiseaseName })
                                      .ToDictionaryAsync(x => x.DiseaseId, x => x.DiseaseName);

            foreach (var r in list)
            {
                if (diseaseMap.TryGetValue(r.DiseaseId, out var name))
                    r.DiseaseName = name;
            }

            return list;
        }

        // ============================================================
        // ✅ BCM Role-based access helpers — mirror DiseaseTrendRepository.
        // Doctor / Admin / Store In-charge see ALL records.
        // Compounder (and any other role) restricted to own CreatedBy.
        // ============================================================
        private static bool IsAdminRole(string? userRole)
            => !string.IsNullOrEmpty(userRole) && userRole.ToLower().Contains("admin");

        private static bool IsStoreRole(string? userRole)
            => !string.IsNullOrEmpty(userRole) && userRole.ToLower().Contains("store");

        private static bool CanSeeAllRecords(bool isDoctor, string? userRole)
            => isDoctor || IsAdminRole(userRole) || IsStoreRole(userRole);

        // Departments: global master (not plant-filtered)
        public async Task<IEnumerable<OrgDepartmentDto>> GetDepartmentsAsync()
        {
            var q = from d in _db.org_departments
                    orderby d.dept_name
                    select new OrgDepartmentDto { DeptId = d.dept_id, DeptName = d.dept_name };

            return await q.ToListAsync();
        }

        public async Task<IEnumerable<MedDiseaseDto>> GetAllDiseasesAsync(int? userPlantId = null)
        {
            var q = _db.MedDiseases.AsQueryable();

            // Filter by plant if the table has a PlantId column and userPlantId is provided
            if (userPlantId.HasValue)
                q = q.Where(d => d.plant_id == userPlantId.Value);

            return await q
                .Select(d => new MedDiseaseDto
                {
                    DiseaseId = d.DiseaseId,
                    DiseaseName = d.DiseaseName
                })
                .OrderBy(d => d.DiseaseName)
                .ToListAsync();
        }

        // ============================================================
        // Bottom-of-report breakdowns
        // ============================================================

        /// <summary>
        /// Disease-wise counts for the VISITORS row:
        /// Automatically displays only Other Diagnosis records where Category = "Others" (or empty/null default).
        /// </summary>
        public async Task<IEnumerable<DiagnosisCensusCountDto>> GetVisitorDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            short? departmentId = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null)
        {
            if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) toDate = DateTime.Now.Date;

            var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

            var createdByFilter = !string.IsNullOrEmpty(currentUserCreatedBy) ? currentUserCreatedBy : currentUserName;
            var applyCreatedByFilter = !CanSeeAllRecords(isDoctor, userRole) && !string.IsNullOrEmpty(createdByFilter);

            // Visitors -> From Other Diagnosis where Category = "Others" (or null/empty)
            var rowsQuery = from dd in _db.OthersDiagnosisDiseases
                            join od in _db.OthersDiagnoses on dd.DiagnosisId equals od.DiagnosisId
                            join op in _db.OtherPatients on od.PatientId equals op.PatientId
                            where od.ApprovalStatus == "Approved"
                                  && (op.Category == null || op.Category.Trim() == "" || op.Category.Trim().ToLower() == "others")
                                  && (!applyCreatedByFilter || od.CreatedBy == createdByFilter || od.CreatedBy == currentUserName)
                                  && od.PlantId == (userPlantId ?? od.PlantId)
                                  && od.VisitDate >= fromDate.Value.Date
                                  && od.VisitDate < toDate.Value.Date.AddDays(1)
                            select new
                            {
                                dd.DiseaseId
                            };

            var rows = await rowsQuery.ToListAsync();

            // Visit-count basis for Other Diagnosis
            var list = rows
                .GroupBy(r => r.DiseaseId)
                .Select(g => new DiagnosisCensusCountDto
                {
                    DeptId = -1,
                    DeptName = "VISITORS",
                    DiseaseId = g.Key,
                    DiseaseName = "",
                    Count = g.LongCount()
                })
                .ToList();

            var diseaseMap = await _db.MedDiseases
                                      .Select(d => new { d.DiseaseId, d.DiseaseName })
                                      .ToDictionaryAsync(x => x.DiseaseId, x => x.DiseaseName);

            foreach (var r in list)
            {
                if (diseaseMap.TryGetValue(r.DiseaseId, out var name))
                    r.DiseaseName = name;
            }

            return list;
        }

        public Task<IEnumerable<DiagnosisCensusCountDto>> GetManagerDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null)
            => GetOtherCategoryDiseaseCountsAsync(
                currentUserName, "manager", "MANAGER", -101,
                fromDate, toDate, isDoctor, userRole, currentUserCreatedBy);

        public Task<IEnumerable<DiagnosisCensusCountDto>> GetEspDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null)
            => GetOtherCategoryDiseaseCountsAsync(
                currentUserName, "esp", "ESP", -102,
                fromDate, toDate, isDoctor, userRole, currentUserCreatedBy);

        private async Task<IEnumerable<DiagnosisCensusCountDto>> GetOtherCategoryDiseaseCountsAsync(
            string currentUserName,
            string categoryName,
            string bucketLabel,
            short bucketDeptId,
            DateTime? fromDate,
            DateTime? toDate,
            bool isDoctor,
            string? userRole,
            string? currentUserCreatedBy)
        {
            if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) toDate = DateTime.Now.Date;

            var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

            var createdByFilter = !string.IsNullOrEmpty(currentUserCreatedBy) ? currentUserCreatedBy : currentUserName;
            var applyCreatedByFilter = !CanSeeAllRecords(isDoctor, userRole) && !string.IsNullOrEmpty(createdByFilter);

            var targetCategory = categoryName.Trim().ToLower();

            var rowsQuery = from dd in _db.OthersDiagnosisDiseases
                            join od in _db.OthersDiagnoses on dd.DiagnosisId equals od.DiagnosisId
                            join op in _db.OtherPatients on od.PatientId equals op.PatientId
                            where od.ApprovalStatus == "Approved"
                                  && op.Category != null
                                  && op.Category.Trim().ToLower() == targetCategory
                                  && (!applyCreatedByFilter || od.CreatedBy == createdByFilter || od.CreatedBy == currentUserName || (currentUserCreatedBy != null && od.CreatedBy == currentUserCreatedBy))
                                  && od.PlantId == (userPlantId ?? od.PlantId)
                                  && od.VisitDate >= fromDate.Value.Date
                                  && od.VisitDate < toDate.Value.Date.AddDays(1)
                            select new
                            {
                                dd.DiseaseId
                            };

            var rows = await rowsQuery.ToListAsync();

            var list = rows
                .GroupBy(r => r.DiseaseId)
                .Select(g => new DiagnosisCensusCountDto
                {
                    DeptId = bucketDeptId,
                    DeptName = bucketLabel,
                    DiseaseId = g.Key,
                    DiseaseName = "",
                    Count = g.LongCount()
                })
                .ToList();

            var diseaseMap = await _db.MedDiseases
                                      .Select(d => new { d.DiseaseId, d.DiseaseName })
                                      .ToDictionaryAsync(x => x.DiseaseId, x => x.DiseaseName);

            foreach (var r in list)
            {
                if (diseaseMap.TryGetValue(r.DiseaseId, out var name))
                    r.DiseaseName = name;
            }

            return list;
        }

        public Task<IEnumerable<DiagnosisCensusCountDto>> GetChildDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null)
            => GetDependentDiseaseCountsAsync(
                currentUserName, fromDate, toDate, isDoctor, userRole, currentUserCreatedBy,
                relations: new[] { "child" }, bucketLabel: "CHILDREN", bucketDeptId: -2);

        public Task<IEnumerable<DiagnosisCensusCountDto>> GetSpouseDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null)
            => GetDependentDiseaseCountsAsync(
                currentUserName, fromDate, toDate, isDoctor, userRole, currentUserCreatedBy,
                relations: new[] { "wife", "husband" }, bucketLabel: "SPOUSE", bucketDeptId: -3);

        /// <summary>
        /// Shared implementation for Children/Spouse — disease-wise VISIT counts (not distinct) for
        /// MedPrescription rows where DependentName refers to an active HrEmployeeDependent whose
        /// relation matches the given set. relations must be lower-case.
        /// </summary>
        private async Task<IEnumerable<DiagnosisCensusCountDto>> GetDependentDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate,
            DateTime? toDate,
            bool isDoctor,
            string? userRole,
            string? currentUserCreatedBy,
            string[] relations,
            string bucketLabel,
            short bucketDeptId)
        {
            if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) toDate = DateTime.Now.Date;

            var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

            var createdByFilter = !string.IsNullOrEmpty(currentUserCreatedBy) ? currentUserCreatedBy : currentUserName;
            var applyCreatedByFilter = !CanSeeAllRecords(isDoctor, userRole) && !string.IsNullOrEmpty(createdByFilter);

            var rowsQuery = from pd in _db.MedPrescriptionDiseases
                            join p in _db.MedPrescriptions on pd.PrescriptionId equals p.PrescriptionId
                            join dep in _db.HrEmployeeDependents on new { p.emp_uid, DepName = p.DependentName } equals new { dep.emp_uid, DepName = dep.dep_name }
                            where p.ApprovalStatus == "Approved"
                                  && p.DependentName != null && p.DependentName != "Self"
                                  && dep.is_active
                                  && relations.Contains(dep.relation.ToLower())
                                  && (!applyCreatedByFilter || p.CreatedBy == createdByFilter || p.CreatedBy == currentUserName)
                                  && p.PlantId == (userPlantId ?? p.PlantId)
                                  && p.PrescriptionDate >= fromDate.Value.Date
                                  && p.PrescriptionDate < toDate.Value.Date.AddDays(1)
                            select new
                            {
                                pd.DiseaseId
                            };

            var rows = await rowsQuery.ToListAsync();

            // Visit-count basis (every diagnosis record counts, no distinct) per confirmed requirement.
            var list = rows
                .GroupBy(r => r.DiseaseId)
                .Select(g => new DiagnosisCensusCountDto
                {
                    DeptId = bucketDeptId,
                    DeptName = bucketLabel,
                    DiseaseId = g.Key,
                    DiseaseName = "",
                    Count = g.LongCount()
                })
                .ToList();

            var diseaseMap = await _db.MedDiseases
                                      .Select(d => new { d.DiseaseId, d.DiseaseName })
                                      .ToDictionaryAsync(x => x.DiseaseId, x => x.DiseaseName);

            foreach (var r in list)
            {
                if (diseaseMap.TryGetValue(r.DiseaseId, out var name))
                    r.DiseaseName = name;
            }

            return list;
        }

    }
}