using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class HrEmployeeDependentRepository : IHrEmployeeDependentRepository
    {
        private readonly ApplicationDbContext _db;
        public HrEmployeeDependentRepository(ApplicationDbContext db) => _db = db;

        public async Task<IEnumerable<HrEmployeeDependent>> ListWithBaseAsync(int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.HrEmployee)
                .Include(m => m.OrgPlant)
                .OrderBy(m => m.dep_name)
                .ToListAsync();
        }

        public async Task<HrEmployeeDependent?> GetByIdWithBaseAsync(int id, int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(m => m.HrEmployee)
                .Include(m => m.OrgPlant)
                .FirstOrDefaultAsync(m => m.emp_dep_id == id);
        }

        // Modified to return only married employees from user's plant
        public async Task<IEnumerable<HrEmployee>> GetBaseListAsync(int? userPlantId = null)
        {
            var query = _db.HrEmployees
                .Where(e => e.marital_status == true); // Only married employees

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query
                .OrderBy(e => e.emp_name)
                .ToListAsync();
        }

        public async Task<List<HrEmployeeDependent>> ListAsync(int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(d => d.OrgPlant)
                .OrderBy(d => d.dep_name)
                .ToListAsync();
        }

        public async Task<HrEmployeeDependent> GetByIdAsync(int id, int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .Include(d => d.OrgPlant)
                .FirstOrDefaultAsync(d => d.emp_dep_id == id);
        }

        // New method to get active dependents for an employee with plant filtering
        public async Task<List<HrEmployeeDependent>> GetActiveDependentsByEmployeeAsync(int empUid, int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents
                .Where(d => d.emp_uid == empUid && d.is_active);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query.ToListAsync();
        }

        // Updated method to check dependent limits with plant filtering
        public async Task<DependentLimitValidationResult> ValidateDependentLimitsAsync(int empUid, string relation, int? excludeDependentId = null, int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents
                .Where(d => d.emp_uid == empUid && d.is_active);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            // Exclude current dependent if editing
            if (excludeDependentId.HasValue)
            {
                query = query.Where(d => d.emp_dep_id != excludeDependentId.Value);
            }

            var existingDependents = await query.ToListAsync();

            var result = new DependentLimitValidationResult { IsValid = true };

            switch (relation?.ToLower())
            {
                case "wife":
                case "husband":
                    var existingSpouse = existingDependents.FirstOrDefault(d =>
                        d.relation?.ToLower() == "wife" || d.relation?.ToLower() == "husband");

                    if (existingSpouse != null)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Employee can have only one spouse (Wife/Husband).";
                    }
                    break;

                case "child":
                    var existingChildren = existingDependents.Where(d =>
                        d.relation?.ToLower() == "child").Count();

                    if (existingChildren >= 2)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Employee can have maximum 2 children as dependents.";
                    }
                    break;
            }

            result.ExistingDependents = existingDependents;
            return result;
        }

        // Updated method to get children over age limit with plant filtering
        public async Task<List<HrEmployeeDependent>> GetChildrenOverAgeLimitAsync(int? userPlantId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var cutoffDate = today.AddYears(-21);

            var query = _db.HrEmployeeDependents
                .Where(d => d.is_active &&
                           d.relation.ToLower() == "child" &&
                           d.dep_dob.HasValue &&
                           d.dep_dob.Value <= cutoffDate);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query.ToListAsync();
        }

        // Updated method to deactivate children over age limit with plant filtering
        public async Task<int> DeactivateChildrenOverAgeLimitAsync(int? userPlantId = null)
        {
            var childrenOverLimit = await GetChildrenOverAgeLimitAsync(userPlantId);
            var count = 0;

            foreach (var child in childrenOverLimit)
            {
                child.is_active = false;
                child.ModifiedBy = "System - Age Limit Check";
                child.ModifiedOn = GetISTDateTime();
                count++;
            }

            if (count > 0)
            {
                await _db.SaveChangesAsync();
            }

            return count;
        }

        public async Task AddAsync(HrEmployeeDependent entity)
        {
            _db.Set<HrEmployeeDependent>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(HrEmployeeDependent entity, string modifiedBy, DateTime modifiedOn)
        {
            var existingEntity = await _db.Set<HrEmployeeDependent>().FindAsync(entity.emp_dep_id);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.emp_uid = entity.emp_uid;
                existingEntity.dep_name = entity.dep_name;
                existingEntity.dep_dob = entity.dep_dob;
                existingEntity.relation = entity.relation;
                existingEntity.gender = entity.gender;
                existingEntity.is_active = entity.is_active;
                existingEntity.marital_status = entity.marital_status;
                // Note: plant_id should generally not be updated after creation for security reasons

                // Update modification audit fields
                existingEntity.ModifiedBy = modifiedBy;
                existingEntity.ModifiedOn = modifiedOn;

                // Auto-deactivate if child is over age limit
                if (existingEntity.relation?.ToLower() == "child" &&
                    existingEntity.dep_dob.HasValue)
                {
                    var age = CalculateAge(existingEntity.dep_dob.Value);
                    if (age > 21)
                    {
                        existingEntity.is_active = false;
                        existingEntity.ModifiedBy = modifiedBy + " - Auto-deactivated (Age > 21)";
                    }
                }

                await _db.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"HrEmployeeDependent with ID {entity.emp_dep_id} not found.");
            }
        }

        public async Task DeleteAsync(int id, int? userPlantId = null)
        {
            var query = _db.HrEmployeeDependents.AsQueryable();

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            var entity = await query.FirstOrDefaultAsync(d => d.emp_dep_id == id);
            if (entity != null)
            {
                _db.HrEmployeeDependents.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // NEW: Helper methods for plant-based operations
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<bool> IsUserAuthorizedForDependentAsync(int dependentId, int userPlantId)
        {
            return await _db.HrEmployeeDependents.AnyAsync(d => d.emp_dep_id == dependentId && d.plant_id == userPlantId);
        }

        public async Task<bool> IsEmployeeInUserPlantAsync(int empUid, int userPlantId)
        {
            return await _db.HrEmployees.AnyAsync(e => e.emp_uid == empUid && e.plant_id == userPlantId);
        }

        // Helper method to calculate age
        private int CalculateAge(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;
            return age;
        }

        // Helper method to get IST DateTime
        private DateTime GetISTDateTime()
        {
            var utcNow = DateTime.UtcNow;
            var istTimeZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, istTimeZone);
        }
    }

    // Helper class for validation results
    public class DependentLimitValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<HrEmployeeDependent> ExistingDependents { get; set; } = new();
    }
}