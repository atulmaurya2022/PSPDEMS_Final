using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class DepartmentMasterRepository : IDepartmentMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public DepartmentMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<OrgDepartment>> ListAsync() =>
          await _db.org_departments.ToListAsync();

        public async Task<OrgDepartment> GetByIdAsync(short id) =>
          await _db.org_departments.FindAsync(id);

        public async Task AddAsync(OrgDepartment entity)
        {
            _db.Set<OrgDepartment>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(OrgDepartment entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<OrgDepartment>().FindAsync(entity.dept_id);

            if (existingEntity != null)
            {
                // Update your specific fields here
                existingEntity.dept_name = entity.dept_name;
                existingEntity.dept_description = entity.dept_description;
                existingEntity.Remarks = entity.Remarks;
                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (don't change them)

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Entity with ID {entity.dept_id} not found.");
            }
        }

        public async Task DeleteAsync(short id)
        {
            var d = await _db.org_departments.FindAsync(id);
            if (d != null)
            {
                _db.org_departments.Remove(d);
                await _db.SaveChangesAsync();
            }
        }

        // Implement the new method
        public async Task<bool> IsDepartmentNameExistsAsync(string deptName, short? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(deptName)) return false;

            var query = _db.org_departments.Where(d => d.dept_name.ToLower() == deptName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(d => d.dept_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
