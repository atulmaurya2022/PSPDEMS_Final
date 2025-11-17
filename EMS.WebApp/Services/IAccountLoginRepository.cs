using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IAccountLoginRepository
    {
        Task<SysUser?> GetByAdidAsync(string adid);
        Task<SysUser?> GetByEmailAndPasswordAsync(string user_name, string password);
        Task<SysUser?> GetByEmailAsync(string user_name);
        Task UpdateAsync(SysUser user);
        Task UpdateLastActivityAsync(string userName);

    }
}
