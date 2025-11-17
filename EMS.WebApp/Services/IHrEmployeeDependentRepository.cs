using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IHrEmployeeDependentRepository
    {
        // Updated methods with plant filtering
        Task<HrEmployeeDependent?> GetByIdWithBaseAsync(int id, int? userPlantId = null);
        Task<IEnumerable<HrEmployeeDependent>> ListWithBaseAsync(int? userPlantId = null);
        Task<IEnumerable<HrEmployee>> GetBaseListAsync(int? userPlantId = null);
        Task<List<HrEmployeeDependent>> ListAsync(int? userPlantId = null);
        Task<HrEmployeeDependent> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(HrEmployeeDependent d);
        Task UpdateAsync(HrEmployeeDependent entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Updated business rule methods with plant filtering
        Task<List<HrEmployeeDependent>> GetActiveDependentsByEmployeeAsync(int empUid, int? userPlantId = null);
        Task<DependentLimitValidationResult> ValidateDependentLimitsAsync(int empUid, string relation, int? excludeDependentId = null, int? userPlantId = null);
        Task<List<HrEmployeeDependent>> GetChildrenOverAgeLimitAsync(int? userPlantId = null);
        Task<int> DeactivateChildrenOverAgeLimitAsync(int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForDependentAsync(int dependentId, int userPlantId);
        Task<bool> IsEmployeeInUserPlantAsync(int empUid, int userPlantId);
    }
}