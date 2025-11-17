using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface ISysUserRepository
    {

        //private readonly ISysUserRepository _repo;
        Task<SysUser?> GetByIdWithBaseAsync(int id);
        Task<IEnumerable<SysUser>> ListWithBaseAsync();
        Task<IEnumerable<SysUser>> ListWithBaseByPlantAsync(int plantId);
        Task<IEnumerable<SysRole>> GetBaseListAsync();
        Task<IEnumerable<OrgPlant>> GetPlantListAsync();

        Task<List<SysUser>> ListAsync();
        Task<List<SysUser>> ListByPlantAsync(int plantId);
        Task<SysUser> GetByIdAsync(int id);
        Task AddAsync(SysUser u);
        Task UpdateAsync(SysUser entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);

        // Helper methods for role and plant checking
        Task<bool> IsAdminRoleAsync(string roleName);
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<IEnumerable<string>> GetAllRoleNamesAsync();
    }
}