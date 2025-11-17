
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IDashboardService
    {
        Task<DoctorDashboardDto> GetDoctorSummaryAsync(string? userName, string? user, int nearExpiryDays = 30);
    }
}
