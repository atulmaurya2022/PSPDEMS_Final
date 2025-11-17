using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class AccountLoginRepository : IAccountLoginRepository
    {
        private readonly ApplicationDbContext _db;

        public AccountLoginRepository(ApplicationDbContext db) => _db = db;

        public async Task<SysUser?> GetByAdidAsync(string adid)
        {
            return await _db.SysUsers
                .FirstOrDefaultAsync(u => u.adid == adid && u.is_active);
        }

        public async Task<SysUser?> GetByEmailAndPasswordAsync(string user_name, string password)
        {
            return await _db.SysUsers
                .FirstOrDefaultAsync(u => u.full_name == user_name && u.adid == password && u.is_active);
        }

        public async Task<SysUser?> GetByEmailAsync(string user_name)
        {
            return await _db.SysUsers.FirstOrDefaultAsync(u => u.adid == user_name && u.is_active);
        }

        public async Task UpdateAsync(SysUser user)
        {
            _db.SysUsers.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateLastActivityAsync(string userName)
        {
            var user = await _db.SysUsers.FirstOrDefaultAsync(u => u.adid == userName && u.is_active);
            if (user != null)
            {
                user.LastActivityTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
    }
}