using System;
using System.Linq;
using System.Threading.Tasks;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public sealed class CompounderDashboardService : ICompounderDashboardService
    {
        private readonly ICompounderIndentRepository _compounderRepo;
        private readonly IExpiredMedicineRepository _expiredRepo;
        private readonly ApplicationDbContext _db;

        // BCM plant code constant - adjust if your plant code is different
        private const string BCM_PLANT_CODE = "BCM";

        public CompounderDashboardService(
            ICompounderIndentRepository compounderRepo,
            IExpiredMedicineRepository expiredRepo,
            ApplicationDbContext db)
        {
            _compounderRepo = compounderRepo;
            _expiredRepo = expiredRepo;
            _db = db;
        }

        /// <summary>
        /// Checks if the given plant ID corresponds to BCM plant.
        /// </summary>
        public async Task<bool> IsBcmPlantAsync(int? plantId)
        {
            if (!plantId.HasValue) return false;

            var plantCode = await _db.org_plants
                .Where(p => p.plant_id == plantId.Value)
                .Select(p => p.plant_code)
                .FirstOrDefaultAsync();

            return !string.IsNullOrEmpty(plantCode) &&
                   plantCode.Equals(BCM_PLANT_CODE, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the plant code for a given plant ID.
        /// </summary>
        public async Task<string?> GetPlantCodeAsync(int? plantId)
        {
            if (!plantId.HasValue) return null;

            return await _db.org_plants
                .Where(p => p.plant_id == plantId.Value)
                .Select(p => p.plant_code)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets the user's role name.
        /// </summary>
        public async Task<string?> GetUserRoleAsync(string? userName)
        {
            if (string.IsNullOrEmpty(userName)) return null;

            var user = await _db.SysUsers
                .Include(u => u.SysRole)
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.SysRole?.role_name;
        }

        public async Task<CompounderDashboardDto> GetSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, int lowStockFallback = 10)
        {
            int? plantId = null;
            if (!string.IsNullOrWhiteSpace(user))
                plantId = await _compounderRepo.GetUserPlantIdAsync(user);

            // Check if this is BCM plant - if yes, filter by user
            bool isBcm = await IsBcmPlantAsync(plantId);

            // Get user role for ExpiredMedicine repository calls
            string? userRole = await GetUserRoleAsync(user);

            var today = DateTime.Today;
            var upto = today.AddDays(nearExpiryDays);

            // For BCM: pass currentUser to filter by CreatedBy
            // For other plants: pass null to show all records
            string? filterUser = isBcm ? userName : null;

            var pendingIndents = await _compounderRepo.ListByStatusAsync("Pending", currentUser: filterUser, userPlantId: plantId);
            int pendingCount = pendingIndents.Count();

            var approvedIndents = await _compounderRepo.ListByStatusAsync("Approved", currentUser: filterUser, userPlantId: plantId);
            int approvedAwaitingReceipt = approvedIndents
                .Where(h => h.CompounderIndentItems != null && h.CompounderIndentItems.Any(i => i.RaisedQuantity > i.ReceivedQuantity))
                .Count();

            // My Drafts - always filtered by current user
            int myDrafts = await _db.CompounderIndents
                .Where(h =>
                    (h.Status == "Draft" || h.IndentType == "Draft Indent") &&
                    (string.IsNullOrEmpty(userName) || h.CreatedBy == userName) &&
                    (!plantId.HasValue || h.plant_id == plantId.Value))
                .CountAsync();

            // Near Expiry Batches - BCM filter by CreatedBy on header
            var nearExpiryQuery = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                            (!plantId.HasValue || x.Header.plant_id == plantId.Value));

            // Apply BCM user filter
            if (isBcm && !string.IsNullOrEmpty(userName))
            {
                nearExpiryQuery = nearExpiryQuery.Where(x => x.Header.CreatedBy == userName);
            }

            int nearExpiryBatches = await nearExpiryQuery.CountAsync();

            // Expired Batches - BCM filter by CreatedBy on header
            var expiredQuery = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate < today &&
                            (!plantId.HasValue || x.Header.plant_id == plantId.Value));

            // Apply BCM user filter
            if (isBcm && !string.IsNullOrEmpty(userName))
            {
                expiredQuery = expiredQuery.Where(x => x.Header.CreatedBy == userName);
            }

            int expiredBatches = await expiredQuery.CountAsync();

            // Expired Pending Disposal - pass userRole AND currentUser for BCM filtering
            // The repository method signature: ListPendingDisposalAsync(int? userPlantId, string? userRole, string? currentUser)
            int expiredPendingDisposal = (await _expiredRepo.ListPendingDisposalAsync(plantId, userRole, filterUser)).Count();

            // Low / OOS in compounder inventory - BCM filter by CreatedBy
            var compStocksQuery = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Where(x => !plantId.HasValue || x.Header.plant_id == plantId.Value);

            // Apply BCM user filter for stock calculations
            if (isBcm && !string.IsNullOrEmpty(userName))
            {
                compStocksQuery = compStocksQuery.Where(x => x.Header.CreatedBy == userName);
            }

            var compStocks = await compStocksQuery
                .GroupBy(x => new { x.i.MedItemId, x.Header.plant_id })
                .Select(g => new
                {
                    g.Key.MedItemId,
                    PlantId = g.Key.plant_id,
                    TotalAvailable = g.Sum(z => (int?)z.b.AvailableStock) ?? 0
                })
                .ToListAsync();

            var medMasters = await _db.med_masters
                .Select(m => new { m.MedItemId, m.MedItemName, ReorderLevel = (int?)m.ReorderLimit })
                .ToListAsync();

            var low = 0; var oos = 0;
            foreach (var s in compStocks)
            {
                var m = medMasters.FirstOrDefault(mm => mm.MedItemId == s.MedItemId);
                int threshold = m != null
                    ? (m.ReorderLevel.HasValue ? m.ReorderLevel.Value : lowStockFallback)
                    : lowStockFallback;

                if (s.TotalAvailable <= 0) oos++;
                else if (s.TotalAvailable <= threshold) low++;
            }

            return new CompounderDashboardDto
            {
                PendingIndents = pendingCount,
                ApprovedAwaitingReceipt = approvedAwaitingReceipt,
                MyDraftIndents = myDrafts,
                NearExpiryBatches = nearExpiryBatches,
                ExpiredBatches = expiredBatches,
                ExpiredMedicinesPendingDisposal = expiredPendingDisposal,
                LowStockCount = low,
                OutOfStockCount = oos,
                NearExpiryDays = nearExpiryDays
            };
        }
    }
}