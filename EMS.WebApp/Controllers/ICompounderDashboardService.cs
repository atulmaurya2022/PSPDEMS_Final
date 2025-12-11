using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface ICompounderDashboardService
    {
        Task<CompounderDashboardDto> GetSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, int lowStockFallback = 10);

        /// <summary>
        /// Checks if the given plant ID corresponds to BCM plant (by plant_code).
        /// </summary>
        Task<bool> IsBcmPlantAsync(int? plantId);

        /// <summary>
        /// Gets the plant code for a given plant ID.
        /// </summary>
        Task<string?> GetPlantCodeAsync(int? plantId);

        /// <summary>
        /// Gets the user's role name.
        /// </summary>
        Task<string?> GetUserRoleAsync(string? userName);
    }
}