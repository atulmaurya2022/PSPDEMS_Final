using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface ISysAttachScreenRoleRepository
    {

        Task<bool> ExistsRoleAsync(int roleUid);
        Task<SysAttachScreenRole?> GetByIdWithBaseAsync(int id);
        Task<List<string>> GetScreenNamesFromCsvAsync(string screenUid);
        //for drop down

        Task<IEnumerable<SysAttachScreenRole>> ListWithBaseAsync();
        Task<IEnumerable<SysRole>> GetRoleListAsync();
        Task<IEnumerable<SysScreenName>> GetScreenListAsync();

        //for uniqueue
        //Task<bool> ExistsAsync(int roleUid, int screenUid);

        Task<List<SysAttachScreenRole>> ListAsync();
        Task<SysAttachScreenRole> GetByIdAsync(int id);
        Task AddAsync(SysAttachScreenRole a);
        Task UpdateAsync(SysAttachScreenRole a, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);
    }
}