using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedBaseRepository : IMedBaseRepository
    {
        private readonly ApplicationDbContext _db;
        public MedBaseRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedBase>> ListAsync(int? userPlantId = null)
        {
            var query = _db.Set<MedBase>().AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(b => b.plant_id == userPlantId.Value);
            }

            return await query
                .Include(b => b.OrgPlant)
                .OrderBy(b => b.BaseName)
                .ToListAsync();
        }

        public async Task<MedBase?> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.Set<MedBase>().AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(b => b.plant_id == userPlantId.Value);
            }

            return await query
                .Include(b => b.OrgPlant)
                .FirstOrDefaultAsync(b => b.BaseId == id);
        }

        public async Task AddAsync(MedBase entity)
        {
            _db.Set<MedBase>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedBase entity, string modifiedBy, DateTime modifiedOn)
        {
            // Get the existing entity from the database
            var existingEntity = await _db.Set<MedBase>().FindAsync(entity.BaseId);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.BaseName = entity.BaseName;
                existingEntity.BaseDesc = entity.BaseDesc;
                // Note: plant_id should generally not be updated after creation for security reasons

                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (they should not be changed)
                // No need to explicitly set them as they're already in existingEntity

                // Entity Framework will track only the changed properties
                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"MedBase with ID {entity.BaseId} not found.");
            }
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.Set<MedBase>().AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(b => b.plant_id == userPlantId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(b => b.BaseId == id);
            if (entity != null)
            {
                _db.Set<MedBase>().Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Updated duplicate check method with plant filtering
        public async Task<bool> IsBaseNameExistsAsync(string baseName, int? excludeId = null, int? userPlantId = null)
        {
            if (string.IsNullOrWhiteSpace(baseName)) return false;

            var query = _db.Set<MedBase>().Where(b => b.BaseName.ToLower() == baseName.ToLower());

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(b => b.plant_id == userPlantId.Value);
            }

            if (excludeId.HasValue)
            {
                query = query.Where(b => b.BaseId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        // NEW: Helper methods for plant-based operations
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<bool> IsUserAuthorizedForBaseAsync(int baseId, int userPlantId)
        {
            return await _db.Set<MedBase>().AnyAsync(b => b.BaseId == baseId && b.plant_id == userPlantId);
        }
    }
}