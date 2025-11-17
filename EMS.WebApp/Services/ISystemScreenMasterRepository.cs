using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface ISystemScreenMasterRepository
    {
        Task<ScreenUpdateResult> UpdateIfControllerExistsAsync(SysScreenName d, string modifiedBy, DateTime modifiedOn);
        Task<ScreenUpdateResult> AddIfControllerExistsAsync(SysScreenName d);

        Task<List<SysScreenName>> ListAsync();
        Task<SysScreenName> GetByIdAsync(int id);
        Task AddAsync(SysScreenName d);
        Task UpdateAsync(SysScreenName d, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(int id);
    }
}