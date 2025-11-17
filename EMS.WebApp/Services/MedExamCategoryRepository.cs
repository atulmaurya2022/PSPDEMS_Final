using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedExamCategoryRepository : IMedExamCategoryRepository
    {
        private readonly ApplicationDbContext _db;
        public MedExamCategoryRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedExamCategory>> ListAsync() =>
            await _db.Set<MedExamCategory>()
                .Include(m => m.med_criteria)
                .ToListAsync();

        public async Task<MedExamCategory?> GetByIdAsync(int id) =>
            await _db.Set<MedExamCategory>().FindAsync(id);

        public async Task<MedExamCategory?> GetByIdWithBaseAsync(int id) =>
            await _db.Set<MedExamCategory>()
                .Include(m => m.med_criteria)
                .FirstOrDefaultAsync(m => m.CatId == id);

        public async Task<IEnumerable<MedCriteria>> GetMedCriteriaListAsync()
        {
            return await _db.Set<MedCriteria>()
                .OrderBy(c => c.criteria_name)
                .ToListAsync();
        }

        public async Task AddAsync(MedExamCategory entity)
        {
            _db.Set<MedExamCategory>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedExamCategory entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<MedExamCategory>().FindAsync(entity.CatId);

            if (existingEntity != null)
            {
                // Update your specific fields here
                existingEntity.CatName = entity.CatName;
                existingEntity.YearsFreq = entity.YearsFreq;
                existingEntity.AnnuallyRule = entity.AnnuallyRule;
                existingEntity.MonthsSched = entity.MonthsSched;
                existingEntity.criteria_id = entity.criteria_id; // New field
                existingEntity.Remarks = entity.Remarks;

                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (don't change them)

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Entity with ID {entity.CatId} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var ent = await _db.Set<MedExamCategory>().FindAsync(id);
            if (ent != null)
            {
                _db.Set<MedExamCategory>().Remove(ent);
                await _db.SaveChangesAsync();
            }
        }

        // Updated composite uniqueness check to include criteria_id
        public async Task<bool> IsCategoryDetailsExistsAsync(string catName, byte yearsFreq, string annuallyRule, string monthsSched, short criteriaId, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(catName) || string.IsNullOrWhiteSpace(annuallyRule) || string.IsNullOrWhiteSpace(monthsSched))
                return false;

            var query = _db.Set<MedExamCategory>().Where(c =>
                c.CatName.ToLower() == catName.ToLower() &&
                c.YearsFreq == yearsFreq &&
                c.AnnuallyRule.ToLower() == annuallyRule.ToLower() &&
                c.MonthsSched.ToLower() == monthsSched.ToLower() &&
                c.criteria_id == criteriaId);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.CatId != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}