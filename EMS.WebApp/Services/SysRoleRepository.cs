using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class SysRoleRepository : ISysRoleRepository
    {
        private readonly ApplicationDbContext _db;
        public SysRoleRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<SysRole>> ListAsync() =>
          await _db.SysRoles.ToListAsync();

        public async Task<SysRole> GetByIdAsync(int id) =>
          await _db.SysRoles.FindAsync(id);

        public async Task AddAsync(SysRole r)
        {
            _db.SysRoles.Add(r);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysRole entity, string modifiedBy, DateTime modifiedOn)
        {
            // Get the existing entity from the database
            var existingEntity = await _db.SysRoles.FindAsync(entity.role_id);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.role_name = entity.role_name;
                existingEntity.role_desc = entity.role_desc;

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
                throw new InvalidOperationException($"SysRole with ID {entity.role_id} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var r = await _db.SysRoles.FindAsync(id);
            if (r != null) { _db.SysRoles.Remove(r); await _db.SaveChangesAsync(); }
        }

        // Implement the new method
        public async Task<bool> IsRoleNameExistsAsync(string roleName, int? excludeId = null)
        {
            var query = _db.SysRoles.Where(r => r.role_name.ToLower() == roleName.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(r => r.role_id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}