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

        public CompounderDashboardService(
            ICompounderIndentRepository compounderRepo,
            IExpiredMedicineRepository expiredRepo,
            ApplicationDbContext db)
        {
            _compounderRepo = compounderRepo;
            _expiredRepo = expiredRepo;
            _db = db;
        }

        public async Task<CompounderDashboardDto> GetSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, int lowStockFallback = 10)
        {
            int? plantId = null;
            if (!string.IsNullOrWhiteSpace(user))
                plantId = await _compounderRepo.GetUserPlantIdAsync(user);

            var today = DateTime.Today;
            var upto = today.AddDays(nearExpiryDays);

            var pendingIndents = await _compounderRepo.ListByStatusAsync("Pending", currentUser: null, userPlantId: plantId);
            int pendingCount = pendingIndents.Count();

            var approvedIndents = await _compounderRepo.ListByStatusAsync("Approved", currentUser: null, userPlantId: plantId);
            int approvedAwaitingReceipt = approvedIndents
                .Where(h => h.CompounderIndentItems != null && h.CompounderIndentItems.Any(i => i.RaisedQuantity > i.ReceivedQuantity))
                .Count();

            int myDrafts = await _db.CompounderIndents
                .Where(h =>
                    (h.Status == "Draft" || h.IndentType == "Draft Indent") &&
                    (string.IsNullOrEmpty(userName) || h.CreatedBy == userName) &&
                    (!plantId.HasValue || h.plant_id == plantId.Value))
                .CountAsync();

            // NOTE: In your model, AvailableStock and ExpiryDate are NON-nullable (int, DateTime)
            int nearExpiryBatches = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                            (!plantId.HasValue || x.Header.plant_id == plantId.Value))
                .CountAsync();

            int expiredBatches = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate < today &&
                            (!plantId.HasValue || x.Header.plant_id == plantId.Value))
                .CountAsync();

            int expiredPendingDisposal = (await _expiredRepo.ListPendingDisposalAsync(plantId)).Count();

            // Low / OOS in compounder inventory
            var compStocks = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .GroupBy(x => new { x.i.MedItemId, x.Header.plant_id })
                .Select(g => new
                {
                    g.Key.MedItemId,
                    PlantId = g.Key.plant_id,
                    // EF Sum(int) -> int?; coalesce to 0 safely
                    TotalAvailable = g.Sum(z => (int?)z.b.AvailableStock) ?? 0
                })
                .Where(s => !plantId.HasValue || s.PlantId == plantId.Value)
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
