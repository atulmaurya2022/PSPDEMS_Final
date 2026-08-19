// File: Services/Reports/IDiagnosisCensusReportService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services.Reports
{
    public interface IDiagnosisCensusReportService
    {
        Task<IEnumerable<DiagnosisCensusCountDto>> GetDiagnosisCensusCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            short? departmentId = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null);
        Task<IEnumerable<OrgDepartmentDto>> GetDepartmentsAsync();

        Task<IEnumerable<MedDiseaseDto>> GetAllDiseasesAsync(int? userPlantId = null);

        // ============================================================
        // NEW: Bottom-of-report breakdowns — Visitors / Children / Spouse / Employee category
        // ============================================================

        /// <summary>
        /// Disease-wise visit counts for the VISITORS row (Category = "Others" from Other Diagnosis).
        /// </summary>
        Task<IEnumerable<DiagnosisCensusCountDto>> GetVisitorDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            short? departmentId = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null);

        /// <summary>
        /// Disease-wise visit counts for the MANAGER row (Category = "Manager" from Other Diagnosis).
        /// </summary>
        Task<IEnumerable<DiagnosisCensusCountDto>> GetManagerDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null);

        /// <summary>
        /// Disease-wise visit counts for the ESP row (Category = "ESP" from Other Diagnosis).
        /// </summary>
        Task<IEnumerable<DiagnosisCensusCountDto>> GetEspDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null);

        /// <summary>
        /// Disease-wise visit counts for dependent prescriptions where relation = Child.
        /// </summary>
        Task<IEnumerable<DiagnosisCensusCountDto>> GetChildDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null);

        /// <summary>
        /// Disease-wise visit counts for dependent prescriptions where relation = Wife/Husband.
        /// </summary>
        Task<IEnumerable<DiagnosisCensusCountDto>> GetSpouseDiseaseCountsAsync(
            string currentUserName,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool isDoctor = false,
            string? userRole = null,
            string? currentUserCreatedBy = null);
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