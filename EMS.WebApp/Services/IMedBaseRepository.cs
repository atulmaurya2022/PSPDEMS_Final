using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedBaseRepository
    {
        // Updated methods with plant filtering
        Task<List<MedBase>> ListAsync(int? userPlantId = null);
        Task<MedBase?> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(MedBase entity);
        Task UpdateAsync(MedBase entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Updated duplicate check method with plant filtering
        Task<bool> IsBaseNameExistsAsync(string baseName, int? excludeId = null, int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForBaseAsync(int baseId, int userPlantId);
    }
}