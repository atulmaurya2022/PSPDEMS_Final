// File: Services/Reports/IDiagnosisCensusReportService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services.Reports
{
    public interface IDiagnosisCensusReportService
    {
        Task<IEnumerable<DiagnosisCensusCountDto>> GetDiagnosisCensusCountsAsync(string currentUserName, DateTime? fromDate = null, DateTime? toDate = null, short? departmentId = null);
        Task<IEnumerable<OrgDepartmentDto>> GetDepartmentsAsync();

        Task<IEnumerable<MedDiseaseDto>> GetAllDiseasesAsync(int? userPlantId = null);
    }

    public class DiagnosisCensusCountDto
    {
        public short DeptId { get; set; }
        public string DeptName { get; set; } = string.Empty;
        public int DiseaseId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public long Count { get; set; }
    }

    public class OrgDepartmentDto
    {
        public short DeptId { get; set; }
        public string DeptName { get; set; } = string.Empty;
    }

    public class MedDiseaseDto
    {
        public int DiseaseId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
    }
}
