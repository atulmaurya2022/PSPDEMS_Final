using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedAmbulanceMasterRepository
    {
    
        Task<List<MedAmbulanceMaster>> ListAsync();
        Task<MedAmbulanceMaster> GetByIdAsync(int id);
        Task AddAsync(MedAmbulanceMaster d);
        Task UpdateAsync(MedAmbulanceMaster entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);
        // Add this new method for vehicle number uniqueness check
        Task<bool> IsVehicleNumberExistsAsync(string vehicleNo, int? excludeId = null);
    }
}
