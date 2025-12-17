using EMS.WebApp.Data;
using EMS.WebApp.Models;

namespace EMS.WebApp.Services
{
    public interface IDiseaseTrendRepository
    {
        #region Common Methods

        /// <summary>
        /// Gets the user's plant ID based on username
        /// </summary>
        Task<int?> GetUserPlantIdAsync(string userName);

        /// <summary>
        /// Gets plant code by plant ID
        /// </summary>
        Task<string?> GetPlantCodeByIdAsync(int plantId);

        /// <summary>
        /// Gets plant name by plant ID
        /// </summary>
        Task<string?> GetPlantNameByIdAsync(int plantId);

        /// <summary>
        /// Gets all filter dropdown options
        /// </summary>
        Task<DiseaseTrendFilterOptions> GetFilterOptionsAsync(int? userPlantId = null);

        /// <summary>
        /// Gets departments for dropdown
        /// </summary>
        Task<List<DropdownItem>> GetDepartmentsAsync();

        /// <summary>
        /// Gets diseases for dropdown
        /// </summary>
        Task<List<DropdownItem>> GetDiseasesAsync(int? userPlantId = null);

        /// <summary>
        /// Gets plants for dropdown
        /// </summary>
        Task<List<DropdownItem>> GetPlantsAsync();

        /// <summary>
        /// Gets employee types for dropdown
        /// </summary>
        Task<List<DropdownItem>> GetEmployeeTypesAsync();

        #endregion

        #region Age Wise Report

        /// <summary>
        /// Gets disease trend analysis by age group
        /// </summary>
        Task<DiseaseTrendAgeWiseReportResponse> GetDiseaseTrendAgeWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null);

        #endregion

        #region Department Wise Report

        /// <summary>
        /// Gets disease trend analysis by department
        /// </summary>
        Task<DiseaseTrendDeptWiseReportResponse> GetDiseaseTrendDeptWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null);

        #endregion

        #region Patient Wise Report

        /// <summary>
        /// Gets disease trend analysis by patient
        /// </summary>
        Task<DiseaseTrendPatientWiseReportResponse> GetDiseaseTrendPatientWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            int page = 1,
            int pageSize = 100);

        #endregion

        #region Medicine Wise Report

        /// <summary>
        /// Gets disease trend analysis by medicine (Medicines Consumption)
        /// </summary>
        Task<DiseaseTrendMedicineWiseReportResponse> GetDiseaseTrendMedicineWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null);

        #endregion

        #region Export Methods

        /// <summary>
        /// Exports Age Wise report to Excel
        /// </summary>
        Task<byte[]> ExportAgeWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null);

        /// <summary>
        /// Exports Department Wise report to Excel
        /// </summary>
        Task<byte[]> ExportDeptWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null);

        /// <summary>
        /// Exports Patient Wise report to Excel
        /// </summary>
        Task<byte[]> ExportPatientWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null);

        /// <summary>
        /// Exports Medicine Wise report to Excel
        /// </summary>
        Task<byte[]> ExportMedicineWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null);

        #endregion
    }
}
