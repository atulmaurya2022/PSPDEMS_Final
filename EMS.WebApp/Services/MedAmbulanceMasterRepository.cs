using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedAmbulanceMasterRepository : IMedAmbulanceMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public MedAmbulanceMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedAmbulanceMaster>> ListAsync() =>
          await _db.med_ambulance_masters.ToListAsync();

        public async Task<MedAmbulanceMaster> GetByIdAsync(int id) =>
          await _db.med_ambulance_masters.FindAsync(id);

        public async Task AddAsync(MedAmbulanceMaster entity)
        {
            _db.Set<MedAmbulanceMaster>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedAmbulanceMaster entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<MedAmbulanceMaster>().FindAsync(entity.amb_id);

            if (existingEntity != null)
            {
                // Update your specific fields here
                existingEntity.vehicle_no = entity.vehicle_no;
                existingEntity.provider = entity.provider;
                existingEntity.vehicle_type = entity.vehicle_type;
                existingEntity.max_capacity = entity.max_capacity;
                existingEntity.is_active = entity.is_active;

                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (don't change them)

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Entity with ID {entity.amb_id} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.med_ambulance_masters.FindAsync(id);
            if (d != null) { _db.med_ambulance_masters.Remove(d); await _db.SaveChangesAsync(); }
        }
        // Implement the new method for vehicle number uniqueness check
        public async Task<bool> IsVehicleNumberExistsAsync(string vehicleNo, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(vehicleNo))
                return false;

            var query = _db.med_ambulance_masters.Where(a =>
                a.vehicle_no.ToLower() == vehicleNo.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(a => a.amb_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
