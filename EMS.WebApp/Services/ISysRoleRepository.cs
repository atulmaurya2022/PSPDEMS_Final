using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface ISysRoleRepository
    {
        Task<List<SysRole>> ListAsync();
        Task<SysRole> GetByIdAsync(int id);
        Task AddAsync(SysRole r);
        Task UpdateAsync(SysRole r, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);
        // Add this new method
        Task<bool> IsRoleNameExistsAsync(string roleName, int? excludeId = null);
    }
}