
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IStoreDashboardService
    {
        Task<StoreDashboardDto> GetSummaryAsync(string? userName,string? user, int nearExpiryDays = 30, int lowStockFallback = 10);
    }
}
