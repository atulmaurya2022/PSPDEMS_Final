using System.Collections.Generic;
using System.Linq;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class MedRefHospitalRepository : IMedRefHospitalRepository
    {
        private readonly ApplicationDbContext _db;
        public MedRefHospitalRepository(ApplicationDbContext db) => _db = db;


        public async Task<List<MedRefHospital>> ListAsync() =>
          await _db.MedRefHospital.ToListAsync();

        public async Task<MedRefHospital> GetByIdAsync(int id) =>
          await _db.MedRefHospital.FindAsync(id);

        public async Task AddAsync(MedRefHospital entity)
        {
            _db.Set<MedRefHospital>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedRefHospital entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<MedRefHospital>().FindAsync(entity.hosp_id);

            if (existingEntity != null)
            {
                // Update your specific fields here
                existingEntity.hosp_name = entity.hosp_name;
                existingEntity.hosp_code = entity.hosp_code;
                existingEntity.speciality = entity.speciality;
                existingEntity.address = entity.address;
                existingEntity.description = entity.description;
                existingEntity.tax_category = entity.tax_category;
                existingEntity.vendor_name = entity.vendor_name;
                existingEntity.vendor_code = entity.vendor_code;
                existingEntity.contact_person_name = entity.contact_person_name;
                existingEntity.contact_person_email_id = entity.contact_person_email_id;
                existingEntity.mobile_number_1 = entity.mobile_number_1;
                existingEntity.mobile_number_2 = entity.mobile_number_2;
                existingEntity.phone_number_1 = entity.phone_number_1;
                existingEntity.phone_number_2 = entity.phone_number_2;
                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (don't change them)

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Entity with ID {entity.hosp_id} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var h = await _db.MedRefHospital.FindAsync(id);
            if (h != null) { _db.MedRefHospital.Remove(h); await _db.SaveChangesAsync(); }
        }
        // Implement the new method for composite uniqueness check
        public async Task<bool> IsHospitalNameCodeExistsAsync(string hospName, string hospCode, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(hospName) || string.IsNullOrWhiteSpace(hospCode))
                return false;

            var query = _db.Set<MedRefHospital>().Where(h =>
                h.hosp_name.ToLower() == hospName.ToLower() &&
                h.hosp_code.ToLower() == hospCode.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(h => h.hosp_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}