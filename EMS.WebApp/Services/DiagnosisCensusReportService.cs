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
        public async Task<IEnumerable<DiagnosisCensusCountDto>> GetDiagnosisCensusCountsAsync(string currentUserName, DateTime? fromDate = null, DateTime? toDate = null, short? departmentId = null)
        {
            if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) toDate = DateTime.Now.Date;

            var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);

            // Prescription-based counts
            var presCounts = from pd in _db.MedPrescriptionDiseases
                             join p in _db.MedPrescriptions on pd.PrescriptionId equals p.PrescriptionId
                             join emp in _db.HrEmployees on p.emp_uid equals emp.emp_uid
                             join dept in _db.org_departments on emp.dept_id equals dept.dept_id
                             where p.ApprovalStatus == "Approved"
                                   && p.PlantId == (userPlantId ?? p.PlantId)
                                   && p.PrescriptionDate >= fromDate.Value.Date
                                   && p.PrescriptionDate < toDate.Value.Date.AddDays(1) // half-open safe-range
                                   && (departmentId == null || emp.dept_id == departmentId)
                             group pd by new { dept.dept_id, dept.dept_name, pd.DiseaseId } into g
                             select new DiagnosisCensusCountDto
                             {
                                 DeptId = g.Key.dept_id,
                                 DeptName = g.Key.dept_name,
                                 DiseaseId = g.Key.DiseaseId,
                                 DiseaseName = "",
                                 Count = g.LongCount()
                             };

            var grouped = from p in presCounts
                          group p by new { p.DeptId, p.DeptName, p.DiseaseId } into gg
                          select new DiagnosisCensusCountDto
                          {
                              DeptId = gg.Key.DeptId,
                              DeptName = gg.Key.DeptName,
                              DiseaseId = gg.Key.DiseaseId,
                              DiseaseName = "",
                              Count = gg.Sum(x => x.Count)
                          };

            var list = await grouped.ToListAsync();

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
