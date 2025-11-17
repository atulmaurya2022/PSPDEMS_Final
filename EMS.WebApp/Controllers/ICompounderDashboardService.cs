using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface ICompounderDashboardService
    {
        Task<CompounderDashboardDto> GetSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, int lowStockFallback = 10);
    }
}
