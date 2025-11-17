using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IDepartmentMasterRepository
    {

        Task<List<OrgDepartment>> ListAsync();
        Task<OrgDepartment> GetByIdAsync(short id);
        Task AddAsync(OrgDepartment d);
        Task UpdateAsync(OrgDepartment entity, string modifiedBy, DateTime modifiedOn);
        Task DeleteAsync(short id);

        // Add this new method
        Task<bool> IsDepartmentNameExistsAsync(string deptName, short? excludeId = null);
    }
}
