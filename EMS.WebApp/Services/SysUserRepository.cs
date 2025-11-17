using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class SysUserRepository : ISysUserRepository
    {
        private readonly ApplicationDbContext _db;
        public SysUserRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<SysUser>> ListWithBaseAsync()
        {
            return await _db.SysUsers
                .Include(m => m.SysRole)
                .Include(m => m.OrgPlant)
                .ToListAsync();
        }

        public async Task<IEnumerable<SysUser>> ListWithBaseByPlantAsync(int plantId)
        {
            return await _db.SysUsers
                .Include(m => m.SysRole)
                .Include(m => m.OrgPlant)
                .Where(m => m.plant_id == plantId)
                .ToListAsync();
        }

        public async Task<SysUser?> GetByIdWithBaseAsync(int id)
        {
            return await _db.SysUsers
                .Include(m => m.SysRole)
                .Include(m => m.OrgPlant)
                .FirstOrDefaultAsync(m => m.user_id == id);
        }

        public async Task<IEnumerable<SysRole>> GetBaseListAsync()
        {
            return await _db.SysRoles.ToListAsync();
        }

        public async Task<IEnumerable<OrgPlant>> GetPlantListAsync()
        {
            return await _db.org_plants.ToListAsync();
        }

        public async Task<List<SysUser>> ListAsync() =>
          await _db.SysUsers.ToListAsync();

        public async Task<List<SysUser>> ListByPlantAsync(int plantId) =>
          await _db.SysUsers.Where(u => u.plant_id == plantId).ToListAsync();

        public async Task<SysUser> GetByIdAsync(int id) =>
          await _db.SysUsers.FindAsync(id);

        public async Task AddAsync(SysUser entity)
        {
            _db.Set<SysUser>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysUser u)
        {
            _db.SysUsers.Update(u);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysUser entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<SysUser>().FindAsync(entity.user_id);

            if (existingEntity != null)
            {
                existingEntity.adid = entity.adid;
                existingEntity.role_id = entity.role_id;
                existingEntity.plant_id = entity.plant_id;
                existingEntity.full_name = entity.full_name;
                existingEntity.email = entity.email;
                existingEntity.is_active = entity.is_active;
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"Entity with ID {entity.user_id} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var u = await _db.SysUsers.FindAsync(id);
            if (u != null)
            {
                _db.SysUsers.Remove(u);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<bool> IsAdminRoleAsync(string roleName)
        {
            // DEBUG: Log what we're checking
            Console.WriteLine($"DEBUG - IsAdminRoleAsync called with roleName: '{roleName}'");

            if (string.IsNullOrEmpty(roleName))
            {
                Console.WriteLine($"DEBUG - Role name is null or empty, returning false");
                return false;
            }

            var role = await _db.SysRoles
                .FirstOrDefaultAsync(r => r.role_name.ToLower() == roleName.ToLower());

            if (role == null)
            {
                Console.WriteLine($"DEBUG - Role not found in database: '{roleName}'");
                return false;
            }

            Console.WriteLine($"DEBUG - Found role in database: '{role.role_name}'");

            // More flexible admin role detection - check for various admin patterns
            var isAdmin = role.role_name.ToLower().Contains("admin") ||
                          role.role_name.ToLower().Contains("administrator") ||
                          role.role_name.ToLower() == "superuser" ||
                          role.role_name.ToLower() == "super user" ||
                          role.role_name.ToLower() == "system admin" ||
                          role.role_name.ToLower() == "systemadmin" ||
                          role.role_name.ToLower() == "admin" ||
                          role.role_name.ToLower() == "manager" ||
                          role.role_name.ToLower() == "supervisor";

            Console.WriteLine($"DEBUG - IsAdmin result: {isAdmin} for role: '{role.role_name}'");

            return isAdmin;
        }

        // Add this method to help debug what roles exist
        public async Task<IEnumerable<string>> GetAllRoleNamesAsync()
        {
            var roles = await _db.SysRoles.Select(r => r.role_name).ToListAsync();
            Console.WriteLine($"DEBUG - All roles in database: {string.Join(", ", roles)}");
            return roles;
        }

        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => u.adid == userName && u.is_active);

            return user?.plant_id;
        }
    }
}