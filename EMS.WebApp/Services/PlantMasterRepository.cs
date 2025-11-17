using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class PlantMasterRepository : IPlantMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public PlantMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<OrgPlant>> ListAsync() =>
          await _db.org_plants.ToListAsync();

        public async Task<OrgPlant> GetByIdAsync(short id) =>
          await _db.org_plants.FindAsync(id);

        public async Task AddAsync(OrgPlant d)
        {
            _db.org_plants.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(OrgPlant entity, string modifiedBy, DateTime modifiedOn)
        {
            // Get the existing entity from the database
            var existingEntity = await _db.org_plants.FindAsync(entity.plant_id);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.plant_code = entity.plant_code;
                existingEntity.plant_name = entity.plant_name;
                existingEntity.Description = entity.Description;

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
                throw new InvalidOperationException($"Plant with ID {entity.plant_id} not found.");
            }
        }

        public async Task DeleteAsync(short id)
        {
            var d = await _db.org_plants.FindAsync(id);
            if (d != null) { _db.org_plants.Remove(d); await _db.SaveChangesAsync(); }
        }

        // Implement the new method
        public async Task<bool> IsPlantCodeExistsAsync(string plantCode, short? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(plantCode)) return false;

            var query = _db.org_plants.Where(p => p.plant_code.ToLower() == plantCode.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(p => p.plant_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}