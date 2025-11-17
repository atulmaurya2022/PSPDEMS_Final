using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class HrEmployeeRepository : IHrEmployeeRepository
    {
        private readonly ApplicationDbContext _db;
        public HrEmployeeRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<HrEmployee>> ListWithBaseAsync(int? userPlantId = null)
        {
            var query = _db.HrEmployees.AsQueryable();

            // Plant-wise filtering - employees can only be viewed from user's plant
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.org_department)
                .Include(m => m.org_plant)
                .Include(m => m.org_employee_category)
                .OrderBy(e => e.emp_name)
                .ToListAsync();
        }

        public async Task<HrEmployee?> GetByIdWithBaseAsync(int id, int? userPlantId = null)
        {
            var query = _db.HrEmployees.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query
                .Include(e => e.org_department)
                .Include(e => e.org_plant)
                .Include(e => e.org_employee_category)
                .FirstOrDefaultAsync(e => e.emp_uid == id);
        }

        public async Task<IEnumerable<OrgDepartment>> GetDepartmentListAsync(int? userPlantId = null)
        {
            var query = _db.org_departments.AsQueryable();

            // Optionally filter departments by plant if there's a relationship
            // For now, returning all departments - modify if departments are plant-specific
            return await query.OrderBy(d => d.dept_name).ToListAsync();
        }

        public async Task<IEnumerable<OrgPlant>> GetPlantListAsync(int? userPlantId = null)
        {
            var query = _db.org_plants.AsQueryable();

            // Only show user's plant for assignment
            if (userPlantId.HasValue)
            {
                query = query.Where(p => p.plant_id == userPlantId.Value);
            }

            return await query.OrderBy(p => p.plant_name).ToListAsync();
        }

        public async Task<IEnumerable<OrgEmployeeCategory>> GetEmployeeCategoryListAsync(int? userPlantId = null)
        {
            var query = _db.org_employee_categories.AsQueryable();

            // Optionally filter categories by plant if there's a relationship
            // For now, returning all categories - modify if categories are plant-specific
            return await query.OrderBy(c => c.emp_category_name).ToListAsync();
        }

        public async Task<List<HrEmployee>> ListAsync(int? userPlantId = null)
        {
            var query = _db.HrEmployees.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query
                .OrderBy(e => e.emp_name)
                .ToListAsync();
        }

        public async Task<HrEmployee> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.HrEmployees.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query.FirstOrDefaultAsync(e => e.emp_uid == id);
        }

        public async Task AddAsync(HrEmployee entity)
        {
            _db.Set<HrEmployee>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(HrEmployee entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<HrEmployee>().FindAsync(entity.emp_uid);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.emp_id = entity.emp_id;
                existingEntity.emp_name = entity.emp_name;
                existingEntity.emp_DOB = entity.emp_DOB;
                existingEntity.emp_Gender = entity.emp_Gender;
                existingEntity.emp_Grade = entity.emp_Grade;
                existingEntity.dept_id = entity.dept_id;
                // Note: plant_id should generally not be updated after creation for security reasons
                // but we'll allow it for legitimate transfers
                existingEntity.plant_id = entity.plant_id;
                existingEntity.emp_category_id = entity.emp_category_id;
                existingEntity.emp_blood_Group = entity.emp_blood_Group;
                existingEntity.marital_status = entity.marital_status;

                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Preserve creation audit fields (don't change them)

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"HrEmployee with ID {entity.emp_uid} not found.");
            }
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.HrEmployees.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(e => e.emp_uid == id);
            if (entity != null)
            {
                _db.HrEmployees.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // Updated duplicate check method with plant filtering
        public async Task<bool> IsEmployeeIdExistsAsync(string empId, int? excludeId = null, int? userPlantId = null)
        {
            if (string.IsNullOrWhiteSpace(empId)) return false;

            var query = _db.HrEmployees.Where(e => e.emp_id.ToLower() == empId.ToLower());

            // Plant-wise filtering - check uniqueness within the same plant
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            if (excludeId.HasValue)
            {
                query = query.Where(e => e.emp_uid != excludeId.Value);
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

        public async Task<bool> IsUserAuthorizedForEmployeeAsync(int empUid, int userPlantId)
        {
            return await _db.HrEmployees.AnyAsync(e => e.emp_uid == empUid && e.plant_id == userPlantId);
        }
    }
}