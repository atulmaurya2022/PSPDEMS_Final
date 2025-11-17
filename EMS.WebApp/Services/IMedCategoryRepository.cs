using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedCategoryRepository
    {
        Task<List<MedCategory>> ListAsync();
        Task<MedCategory?> GetByIdAsync(int id);
        Task AddAsync(MedCategory entity);
        Task UpdateAsync(MedCategory entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);
        // Add this new method
        Task<bool> IsCategoryNameExistsAsync(string categoryName, int? excludeId = null);
    }
}
