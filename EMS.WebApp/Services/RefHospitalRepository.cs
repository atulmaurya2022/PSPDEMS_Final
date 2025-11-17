using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class RefHospitalRepository : IRefHospitalRepository
    {
        private readonly ApplicationDbContext _db;
        public RefHospitalRepository(ApplicationDbContext db) => _db = db;

        public async Task<List<MedRefHospital>> ListAsync() =>
            await _db.Set<MedRefHospital>().ToListAsync();

        public async Task<MedRefHospital?> GetByIdAsync(int id) =>
            await _db.Set<MedRefHospital>().FindAsync(id);

        public async Task AddAsync(MedRefHospital entity)
        {
            _db.Set<MedRefHospital>().Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MedRefHospital entity)
        {
            _db.Set<MedRefHospital>().Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.Set<MedRefHospital>().FindAsync(id);
            if (entity != null)
            {
                _db.Set<MedRefHospital>().Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
