using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedDiseaseRepository
    {
        // Updated methods with plant filtering
        Task<List<MedDisease>> ListAsync(int? userPlantId = null);
        Task<MedDisease> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(MedDisease entity);
        Task UpdateAsync(MedDisease entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Updated duplicate check method with plant filtering
        Task<bool> IsDiseaseNameExistsAsync(string diseaseName, int? excludeId = null, int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForDiseaseAsync(int diseaseId, int userPlantId);
    }
}