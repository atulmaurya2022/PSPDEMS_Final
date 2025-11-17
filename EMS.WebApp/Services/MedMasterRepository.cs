using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedMasterRepository : IMedMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public MedMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<MedMaster>> ListWithBaseAsync(int? userPlantId = null)
        {
            var query = _db.med_masters.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.MedBase)
                .Include(m => m.OrgPlant)
                .OrderBy(m => m.MedItemName)
                .ToListAsync();
        }

        public async Task<MedMaster?> GetByIdWithBaseAsync(int id, int? userPlantId = null)
        {
            var query = _db.med_masters.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.MedBase)
                .Include(m => m.OrgPlant)
                .FirstOrDefaultAsync(m => m.MedItemId == id);
        }

        public async Task<IEnumerable<MedBase>> GetBaseListAsync(int? userPlantId = null)
        {
            var query = _db.med_bases.AsQueryable();

            // Plant-wise filtering - only show bases from user's plant
            if (userPlantId.HasValue)
            {
                query = query.Where(b => b.plant_id == userPlantId.Value);
            }

            return await query
                .OrderBy(b => b.BaseName)
                .ToListAsync();
        }

        public async Task<List<MedMaster>> ListAsync(int? userPlantId = null)
        {
            var query = _db.Set<MedMaster>().AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.OrgPlant)
                .OrderBy(m => m.MedItemName)
                .ToListAsync();
        }

        public async Task<MedMaster?> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.Set<MedMaster>().AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.OrgPlant)
                .FirstOrDefaultAsync(m => m.MedItemId == id);
        }

        public async Task AddAsync(MedMaster entity)
        {
            _db.Set<MedMaster>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedMaster entity, string modifiedBy, DateTime modifiedOn)
        {
            // Get the existing entity from the database
            var existingEntity = await _db.Set<MedMaster>().FindAsync(entity.MedItemId);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.MedItemName = entity.MedItemName;
                existingEntity.BaseId = entity.BaseId;
                existingEntity.CompanyName = entity.CompanyName;
                existingEntity.ReorderLimit = entity.ReorderLimit;
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
                throw new InvalidOperationException($"MedMaster with ID {entity.MedItemId} not found.");
            }
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.Set<MedMaster>().AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(m => m.MedItemId == id);
            if (entity != null)
            {
                _db.Set<MedMaster>().Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Updated composite uniqueness check with plant filtering
        public async Task<bool> IsMedItemDetailsExistsAsync(string medItemName, int? baseId, string? companyName, int? excludeId = null, int? userPlantId = null)
        {
            if (string.IsNullOrWhiteSpace(medItemName))
                return false;

            var query = _db.Set<MedMaster>().Where(m =>
                m.MedItemName.ToLower() == medItemName.ToLower() &&
                m.BaseId == baseId);

            // Plant-wise filtering - check uniqueness within the same plant
            if (userPlantId.HasValue)
            {
                query = query.Where(m => m.plant_id == userPlantId.Value);
            }

            // Handle nullable CompanyName comparison
            if (string.IsNullOrWhiteSpace(companyName))
            {
                query = query.Where(m => string.IsNullOrEmpty(m.CompanyName));
            }
            else
            {
                query = query.Where(m => !string.IsNullOrEmpty(m.CompanyName) &&
                                        m.CompanyName.ToLower() == companyName.ToLower());
            }

            if (excludeId.HasValue)
            {
                query = query.Where(m => m.MedItemId != excludeId.Value);
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

        public async Task<bool> IsUserAuthorizedForMedicineAsync(int medItemId, int userPlantId)
        {
            return await _db.Set<MedMaster>().AnyAsync(m => m.MedItemId == medItemId && m.plant_id == userPlantId);
        }
    }
}