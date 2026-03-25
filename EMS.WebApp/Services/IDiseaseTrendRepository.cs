using EMS.WebApp.Data;
using EMS.WebApp.Models;

namespace EMS.WebApp.Services
{
    public interface IDiseaseTrendRepository
    {
        #region Common Methods

        Task<int?> GetUserPlantIdAsync(string userName);
        Task<string?> GetPlantCodeByIdAsync(int plantId);
        Task<string?> GetPlantNameByIdAsync(int plantId);
        Task<DiseaseTrendFilterOptions> GetFilterOptionsAsync(int? userPlantId = null);
        Task<List<DropdownItem>> GetDepartmentsAsync();
        Task<List<DropdownItem>> GetDiseasesAsync(int? userPlantId = null);
        Task<List<DropdownItem>> GetPlantsAsync();

        // ✅ FIX: Employee types now loaded from master table (org_employee_category)
        Task<List<DropdownItem>> GetEmployeeTypesAsync();

        #endregion

        #region Age Wise Report

        // ✅ FIX: Added isDoctor and userRole for role-based access control
        Task<DiseaseTrendAgeWiseReportResponse> GetDiseaseTrendAgeWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        #endregion

        #region Department Wise Report

        // ✅ FIX: Added isDoctor and userRole for role-based access control
        Task<DiseaseTrendDeptWiseReportResponse> GetDiseaseTrendDeptWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        #endregion

        #region Patient Wise Report

        // ✅ FIX: Added isDoctor and userRole for role-based access control
        Task<DiseaseTrendPatientWiseReportResponse> GetDiseaseTrendPatientWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            int page = 1,
            int pageSize = 100,
            bool isDoctor = false,
            string? userRole = null);

        #endregion

        #region Medicine Wise Report

        // ✅ FIX: Added isDoctor and userRole for role-based access control
        Task<DiseaseTrendMedicineWiseReportResponse> GetDiseaseTrendMedicineWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        #endregion

        #region Export Methods

        Task<byte[]> ExportAgeWiseToExcelAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        Task<byte[]> ExportDeptWiseToExcelAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        Task<byte[]> ExportPatientWiseToExcelAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        Task<byte[]> ExportMedicineWiseToExcelAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false,
            string? userRole = null);

        #endregion
    }
}