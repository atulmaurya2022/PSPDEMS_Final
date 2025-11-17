using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedDiagnosisRepository
    {
        // Updated methods with plant filtering
        Task<List<MedDiagnosis>> ListAsync(int? userPlantId = null);
        Task<MedDiagnosis> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(MedDiagnosis d);
        Task UpdateAsync(MedDiagnosis entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Updated duplicate check method with plant filtering
        Task<bool> IsDiagnosisNameExistsAsync(string diagnosisName, int? excludeId = null, int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForDiagnosisAsync(int diagnosisId, int userPlantId);
    }
}