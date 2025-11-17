using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedDiseaseRepository : IMedDiseaseRepository
    {
        private readonly ApplicationDbContext _db;
        public MedDiseaseRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedDisease>> ListAsync(int? userPlantId = null)
        {
            var query = _db.MedDiseases.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(d => d.OrgPlant)
                .OrderBy(d => d.DiseaseName)
                .ToListAsync();
        }

        public async Task<MedDisease> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.MedDiseases.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(d => d.OrgPlant)
                .FirstOrDefaultAsync(d => d.DiseaseId == id);
        }

        public async Task AddAsync(MedDisease entity)
        {
            _db.Set<MedDisease>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedDisease entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<MedDisease>().FindAsync(entity.DiseaseId);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.DiseaseName = entity.DiseaseName;
                existingEntity.DiseaseDesc = entity.DiseaseDesc;
                // Note: plant_id should generally not be updated after creation for security reasons

                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (they should not be changed)
                // No need to explicitly set them as they're already in existingEntity

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"MedDisease with ID {entity.DiseaseId} not found.");
            }
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.MedDiseases.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(d => d.DiseaseId == id);
            if (entity != null)
            {
                _db.MedDiseases.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Updated duplicate check method with plant filtering
        public async Task<bool> IsDiseaseNameExistsAsync(string diseaseName, int? excludeId = null, int? userPlantId = null)
        {
            if (string.IsNullOrWhiteSpace(diseaseName)) return false;

            var query = _db.MedDiseases.Where(d => d.DiseaseName.ToLower() == diseaseName.ToLower());

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.DiseaseId != excludeId.Value);
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

        public async Task<bool> IsUserAuthorizedForDiseaseAsync(int diseaseId, int userPlantId)
        {
            return await _db.MedDiseases.AnyAsync(d => d.DiseaseId == diseaseId && d.plant_id == userPlantId);
        }
    }
}