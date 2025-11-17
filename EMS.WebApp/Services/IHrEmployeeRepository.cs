using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IHrEmployeeRepository
    {
        // Updated methods with plant filtering
        Task<HrEmployee?> GetByIdWithBaseAsync(int id, int? userPlantId = null);
        Task<IEnumerable<HrEmployee>> ListWithBaseAsync(int? userPlantId = null);
        Task<IEnumerable<OrgDepartment>> GetDepartmentListAsync(int? userPlantId = null);
        Task<IEnumerable<OrgPlant>> GetPlantListAsync(int? userPlantId = null);
        Task<IEnumerable<OrgEmployeeCategory>> GetEmployeeCategoryListAsync(int? userPlantId = null);

        Task<List<HrEmployee>> ListAsync(int? userPlantId = null);
        Task<HrEmployee> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(HrEmployee entity);
        Task UpdateAsync(HrEmployee entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Updated duplicate check method with plant filtering
        Task<bool> IsEmployeeIdExistsAsync(string empId, int? excludeId = null, int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForEmployeeAsync(int empUid, int userPlantId);
    }
}