using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IPlantMasterRepository
    {
        Task<List<OrgPlant>> ListAsync();
        Task<OrgPlant> GetByIdAsync(short id);
        Task AddAsync(OrgPlant d);
        Task UpdateAsync(OrgPlant d, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(short id);
        // Add this new method
        Task<bool> IsPlantCodeExistsAsync(string plantCode, short? excludeId = null);
    }
}