using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedMasterRepository
    {
        // Updated methods with plant filtering
        Task<MedMaster?> GetByIdWithBaseAsync(int id, int? userPlantId = null);
        Task<IEnumerable<MedMaster>> ListWithBaseAsync(int? userPlantId = null);
        Task<IEnumerable<MedBase>> GetBaseListAsync(int? userPlantId = null);

        Task<List<MedMaster>> ListAsync(int? userPlantId = null);
        Task<MedMaster?> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(MedMaster entity);
        Task UpdateAsync(MedMaster entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Updated composite uniqueness check with plant filtering
        Task<bool> IsMedItemDetailsExistsAsync(string medItemName, int? baseId, string? companyName, int? excludeId = null, int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForMedicineAsync(int medItemId, int userPlantId);
    }
}