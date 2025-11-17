using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IMedExamCategoryRepository
    {
        Task<List<MedExamCategory>> ListAsync();
        Task<MedExamCategory?> GetByIdAsync(int id);
        Task<MedExamCategory?> GetByIdWithBaseAsync(int id);
        Task AddAsync(MedExamCategory entity);
        Task UpdateAsync(MedExamCategory entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);
        Task<IEnumerable<MedCriteria>> GetMedCriteriaListAsync();

        // Composite uniqueness check - updated to include criteria_id
        Task<bool> IsCategoryDetailsExistsAsync(string catName, byte yearsFreq, string annuallyRule, string monthsSched, short criteriaId, int? excludeId = null);
    }
}