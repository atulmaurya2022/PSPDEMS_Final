using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedDiagnosisRepository : IMedDiagnosisRepository
    {
        private readonly ApplicationDbContext _db;
        public MedDiagnosisRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedDiagnosis>> ListAsync(int? userPlantId = null)
        {
            var query = _db.MedDiagnosis.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(d => d.OrgPlant)
                .OrderBy(d => d.diag_name)
                .ToListAsync();
        }

        public async Task<MedDiagnosis> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.MedDiagnosis.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(d => d.OrgPlant)
                .FirstOrDefaultAsync(d => d.diag_id == id);
        }

        public async Task AddAsync(MedDiagnosis entity)
        {
            _db.Set<MedDiagnosis>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedDiagnosis entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<MedDiagnosis>().FindAsync(entity.diag_id);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.diag_name = entity.diag_name;
                existingEntity.diag_desc = entity.diag_desc;
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
                throw new InvalidOperationException($"MedDiagnosis with ID {entity.diag_id} not found.");
            }
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.MedDiagnosis.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(d => d.diag_id == id);
            if (entity != null)
            {
                _db.MedDiagnosis.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Updated duplicate check method with plant filtering
        public async Task<bool> IsDiagnosisNameExistsAsync(string diagnosisName, int? excludeId = null, int? userPlantId = null)
        {
            if (string.IsNullOrWhiteSpace(diagnosisName)) return false;

            var query = _db.MedDiagnosis.Where(d => d.diag_name.ToLower() == diagnosisName.ToLower());

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.diag_id != excludeId.Value);
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

        public async Task<bool> IsUserAuthorizedForDiagnosisAsync(int diagnosisId, int userPlantId)
        {
            return await _db.MedDiagnosis.AnyAsync(d => d.diag_id == diagnosisId && d.plant_id == userPlantId);
        }
    }
}