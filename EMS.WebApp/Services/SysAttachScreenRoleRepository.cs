using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class SysAttachScreenRoleRepository : ISysAttachScreenRoleRepository
    {
        private readonly ApplicationDbContext _db;
        public SysAttachScreenRoleRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<string>> GetScreenNamesFromCsvAsync(string screenUidCsv)
        {
            if (string.IsNullOrWhiteSpace(screenUidCsv))
                return new List<string>();

            var ids = screenUidCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var i) ? i : 0)
                .Where(id => id > 0)
                .ToList();

            var screenNames = await _db.sys_screen_names
                .Where(s => ids.Contains(s.screen_uid))
                .Select(s => s.screen_name)
                .ToListAsync();

            return screenNames;
        }

        public async Task<bool> ExistsRoleAsync(int roleUid)
        {
            return await _db.SysAttachScreenRoles.AnyAsync(x => x.role_uid == roleUid);
        }
        public async Task<IEnumerable<SysAttachScreenRole>> ListWithBaseAsync()
        {
            var attachments = await _db.SysAttachScreenRoles
                .Include(m => m.SysRole)
                .ToListAsync();

            // Manually populate RelatedScreens for each item
            foreach (var attachment in attachments)
            {
                if (!string.IsNullOrEmpty(attachment.screen_uid))
                {
                    var screenIds = attachment.screen_uids;
                    attachment.RelatedScreens = await _db.sys_screen_names
                        .Where(s => screenIds.Contains(s.screen_uid))
                        .ToListAsync();
                }
            }

            return attachments;
        }

        public async Task<SysAttachScreenRole?> GetByIdWithBaseAsync(int id)
        {
            var attachment = await _db.SysAttachScreenRoles
                .Include(e => e.SysRole)
                .FirstOrDefaultAsync(e => e.uid == id);

            if (attachment != null && !string.IsNullOrEmpty(attachment.screen_uid))
            {
                var screenIds = attachment.screen_uids;
                attachment.RelatedScreens = await _db.sys_screen_names
                    .Where(s => screenIds.Contains(s.screen_uid))
                    .ToListAsync();
            }

            return attachment;
        }

        public async Task<IEnumerable<SysRole>> GetRoleListAsync()
        {
            return await _db.SysRoles.ToListAsync();
        }

        public async Task<IEnumerable<SysScreenName>> GetScreenListAsync()
        {
            return await _db.sys_screen_names.ToListAsync();
        }

        public async Task<bool> ExistsAsync(int roleUid, string screenUid)
        {
            return await _db.SysAttachScreenRoles
                .AnyAsync(x => x.role_uid == roleUid && x.screen_uid == screenUid);
        }

        public async Task<List<SysAttachScreenRole>> ListAsync() =>
          await _db.SysAttachScreenRoles.ToListAsync();

        public async Task<SysAttachScreenRole> GetByIdAsync(int id) =>
          await _db.SysAttachScreenRoles.FindAsync(id);

        public async Task AddAsync(SysAttachScreenRole a)
        {
            _db.SysAttachScreenRoles.Add(a);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(SysAttachScreenRole entity, string modifiedBy, DateTime modifiedOn)
        {
            // Get the existing entity from the database
            var existingEntity = await _db.SysAttachScreenRoles.FindAsync(entity.uid);

            if (existingEntity != null)
            {
                // Update only the fields that should be changed
                existingEntity.role_uid = entity.role_uid;
                existingEntity.screen_uid = entity.screen_uid;

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
                throw new InvalidOperationException($"SysAttachScreenRole with ID {entity.uid} not found.");
            }
        }

        public async Task DeleteAsync(int id)
        {
            var a = await _db.SysAttachScreenRoles.FindAsync(id);
            if (a != null)
            {
                _db.SysAttachScreenRoles.Remove(a);
                await _db.SaveChangesAsync();
            }
        }
    }
}