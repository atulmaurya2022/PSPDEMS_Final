using EMS.WebApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EMS.WebApp.Services
{
    public class SystemScreenMasterRepository : ISystemScreenMasterRepository
    {
        private readonly ApplicationDbContext _db;
        public SystemScreenMasterRepository(ApplicationDbContext db) => _db = db;

        public async Task<ScreenUpdateResult> AddIfControllerExistsAsync(SysScreenName d)
        {
            var controllers = GetAvailableControllerNames();

            if (!controllers.Contains(d.screen_name))
            {
                return new ScreenUpdateResult
                {
                    Success = false,
                    AvailableControllers = controllers
                };
            }

            _db.sys_screen_names.Add(d);
            await _db.SaveChangesAsync();

            return new ScreenUpdateResult { Success = true };
        }

        public async Task<ScreenUpdateResult> UpdateIfControllerExistsAsync(SysScreenName entity, string modifiedBy, DateTime modifiedOn)
        {
            var controllers = GetAvailableControllerNames();

            if (!controllers.Contains(entity.screen_name))
            {
                return new ScreenUpdateResult
                {
                    Success = false,
                    AvailableControllers = controllers
                };
            }

            // Get the existing entity from the database
            var existingEntity = await _db.sys_screen_names.FindAsync(entity.screen_uid);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.screen_name = entity.screen_name;
                existingEntity.screen_description = entity.screen_description;

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
                throw new InvalidOperationException($"sys_screen_name with ID {entity.screen_uid} not found.");
            }

            return new ScreenUpdateResult { Success = true };
        }

        private List<string> GetAvailableControllerNames()
        {
            return Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.Name.Replace("Controller", ""))
                .ToList();
        }

        public async Task<List<SysScreenName>> ListAsync() =>
          await _db.sys_screen_names.ToListAsync();

        public async Task<SysScreenName> GetByIdAsync(int id) =>
          await _db.sys_screen_names.FindAsync(id);

        public async Task AddAsync(SysScreenName d)
        {
            _db.sys_screen_names.Add(d);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysScreenName entity, string modifiedBy, DateTime modifiedOn)
        {
            // Get the existing entity from the database
            var existingEntity = await _db.sys_screen_names.FindAsync(entity.screen_uid);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.screen_name = entity.screen_name;
                existingEntity.screen_description = entity.screen_description;

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
                throw new InvalidOperationException($"sys_screen_name with ID {entity.screen_uid} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var d = await _db.sys_screen_names.FindAsync(id);
            if (d != null) { _db.sys_screen_names.Remove(d); await _db.SaveChangesAsync(); }
        }
    }
}