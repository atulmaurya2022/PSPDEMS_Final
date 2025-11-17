using System.Collections.Generic;
using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedRefHospitalRepository
    {
       

        Task<List<MedRefHospital>> ListAsync();
        Task<MedRefHospital> GetByIdAsync(int id);
        Task AddAsync(MedRefHospital h);
        Task UpdateAsync(MedRefHospital entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);

        // Add this new method for composite uniqueness check
        Task<bool> IsHospitalNameCodeExistsAsync(string hospName, string hospCode, int? excludeId = null);
    }
}
