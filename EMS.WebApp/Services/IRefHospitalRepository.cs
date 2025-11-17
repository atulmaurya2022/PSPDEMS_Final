using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IRefHospitalRepository
    {
        Task<List<MedRefHospital>> ListAsync();
        Task<MedRefHospital?> GetByIdAsync(int id);
        Task AddAsync(MedRefHospital entity);
        Task UpdateAsync(MedRefHospital entity);
        Task DeleteAsync(int id);
    }
}
