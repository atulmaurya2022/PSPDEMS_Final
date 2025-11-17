using System;
using System.Linq;
using System.Threading.Tasks;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public sealed class StoreDashboardService : IStoreDashboardService
    {
        private readonly IStoreIndentRepository _storeRepo;
        private readonly IExpiredMedicineRepository _expiredRepo;
        private readonly ApplicationDbContext _db;

        public StoreDashboardService(
            IStoreIndentRepository storeRepo,
            IExpiredMedicineRepository expiredRepo,
            ApplicationDbContext db)
        {
            _storeRepo = storeRepo;
            _expiredRepo = expiredRepo;
            _db = db;
        }

        public async Task<StoreDashboardDto> GetSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, int lowStockFallback = 10)
        {
            int? plantId = null;
            if (!string.IsNullOrWhiteSpace(user))
                plantId = await _storeRepo.GetUserPlantIdAsync(user);

            var today = DateTime.Today;
            var upto = today.AddDays(nearExpiryDays);

            // Pending indents
            var pendingIndents = await _storeRepo.ListByStatusAsync("Pending", currentUser: null, userPlantId: plantId);
            int pendingCount = pendingIndents.Count();

            // Approved awaiting receipt (any item pending quantity)
            var approvedIndents = await _storeRepo.ListByStatusAsync("Approved", currentUser: null, userPlantId: plantId);
            int approvedAwaitingReceipt = approvedIndents
                .Where(h => h.StoreIndentItems != null && h.StoreIndentItems.Any(i => i.RaisedQuantity > i.ReceivedQuantity))
                .Count();

            // My drafts
            int myDrafts = await _db.StoreIndents
                .Where(h =>
                    (h.Status == "Draft" || h.IndentType == "Draft Indent") &&
                    (string.IsNullOrEmpty(userName) || h.CreatedBy == userName) &&
                    (!plantId.HasValue || h.PlantId == plantId.Value))
                .CountAsync();

            // Near-expiry & expired batches (store)
            int nearExpiryBatches = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                            (!plantId.HasValue || x.Header.PlantId == plantId.Value))
                .CountAsync();

            int expiredBatches = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate < today &&
                            (!plantId.HasValue || x.Header.PlantId == plantId.Value))
                .CountAsync();

            // Central expired medicines (pending disposal)
            int expiredPendingDisposal = (await _expiredRepo.ListPendingDisposalAsync(plantId)).Count();

            // Low/Out-of-stock (group batches by MedItem within plant)
            var storeStocks = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .GroupBy(x => new { x.i.MedItemId, x.Header.PlantId })
                .Select(g => new
                {
                    g.Key.MedItemId,
                    PlantId = g.Key.PlantId,
                    TotalAvailable = g.Sum(z => (int?)z.b.AvailableStock) ?? 0
                })
                .Where(s => !plantId.HasValue || s.PlantId == plantId.Value)
                .ToListAsync();

            // Med masters with reorder limit
            var medMasters = await _db.med_masters
                .Select(m => new { m.MedItemId, m.MedItemName, ReorderLevel = (int?)m.ReorderLimit })
                .ToListAsync();

            var low = 0; var oos = 0;
            foreach (var s in storeStocks)
            {
                var m = medMasters.FirstOrDefault(mm => mm.MedItemId == s.MedItemId);
                int threshold = m?.ReorderLevel ?? lowStockFallback;
                if (s.TotalAvailable <= 0) oos++;
                else if (s.TotalAvailable <= threshold) low++;
            }

            return new StoreDashboardDto
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
