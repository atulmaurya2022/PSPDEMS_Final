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

        // Only med_prescription + med_prescription_disease used (others removed as requested)
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

            // Pull flat (dept, disease, emp_uid) rows — emp_uid required for the DISTINCT count.
            // Materialize-then-group mirrors DT-DeptWise's proven pattern → predictable EF Core translation.
            var rowsQuery = from pd in _db.MedPrescriptionDiseases
                            join p in _db.MedPrescriptions on pd.PrescriptionId equals p.PrescriptionId
                            join d in _db.MedDiseases on pd.DiseaseId equals d.DiseaseId
                            join emp in _db.HrEmployees on p.emp_uid equals emp.emp_uid
                            join dept in _db.org_departments on emp.dept_id equals dept.dept_id
                            where p.ApprovalStatus == "Approved"
                                  && (p.DependentName == null || p.DependentName == "Self")
                                  && (!applyCreatedByFilter || p.CreatedBy == createdByFilter)
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

            var rows = await rowsQuery.ToListAsync();

            // Group in memory: DISTINCT employees per (dept, disease) — matches DT-DeptWise semantics.
            var list = rows
                .GroupBy(r => new { r.dept_id, r.dept_name, r.DiseaseId })
                .Select(g => new DiagnosisCensusCountDto
                {
                    DeptId = g.Key.dept_id,
                    DeptName = g.Key.dept_name,
                    DiseaseId = g.Key.DiseaseId,
                    DiseaseName = "",
                    Count = g.Select(x => x.emp_uid).Distinct().Count()
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

    }
}