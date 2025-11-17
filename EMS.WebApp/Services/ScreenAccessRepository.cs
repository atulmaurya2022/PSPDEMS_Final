using EMS.WebApp.Data;
using System;
using Microsoft.EntityFrameworkCore;
namespace EMS.WebApp.Services
{
    public class ScreenAccessRepository : IScreenAccessRepository
    {
        private readonly ApplicationDbContext _db;
        public ScreenAccessRepository(ApplicationDbContext db) => _db = db;
        public async Task<bool> HasScreenAccessAsync(string userUsername, string screenName)
        {
            var user = await _db.SysUsers
                .Include(u => u.SysRole)
                .FirstOrDefaultAsync(u => (u.full_name == userUsername || u.adid == userUsername) && u.is_active);

            if (user == null || user.role_id == null)
                return false;

            var mappings = await _db.SysAttachScreenRoles
                .Where(m => m.role_uid == user.role_id)
                .ToListAsync();

            var screenList = await _db.sys_screen_names.ToListAsync();

            foreach (var mapping in mappings)
            {
                var screenIds = mapping.screen_uid
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id, out var i) ? i : 0)
                    .Where(id => id > 0)
                    .ToList();

                foreach (var id in screenIds)
                {
                    var screen = screenList.FirstOrDefault(s => s.screen_uid == id);
                    if (screen != null && screen.screen_name.Equals(screenName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
