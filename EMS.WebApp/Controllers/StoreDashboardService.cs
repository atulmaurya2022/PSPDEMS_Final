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

        public async Task<StoreDashboardDto> GetSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, int lowStockFallback = 10, DateTime? fromDate = null, DateTime? toDate = null)
        {
            int? plantId = null;
            if (!string.IsNullOrWhiteSpace(user))
                plantId = await _storeRepo.GetUserPlantIdAsync(user);

            var today = DateTime.Today;
            var upto = today.AddDays(nearExpiryDays);

            // Pending indents — filtered by IndentDate range via EF
            var pendingQ = _db.StoreIndents
                .Where(h => h.Status == "Pending" &&
                            (!plantId.HasValue || h.PlantId == plantId.Value) &&
                            (!fromDate.HasValue || h.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || h.IndentDate <= toDate.Value));
            int pendingCount = await pendingQ.CountAsync();

            // Approved awaiting receipt — filtered by IndentDate range via EF
            var approvedQ = _db.StoreIndents
                .Include(h => h.StoreIndentItems)
                .Where(h => h.Status == "Approved" &&
                            (!plantId.HasValue || h.PlantId == plantId.Value) &&
                            (!fromDate.HasValue || h.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || h.IndentDate <= toDate.Value));
            var approvedIndents = await approvedQ.ToListAsync();
            int approvedAwaitingReceipt = approvedIndents
                .Where(h => h.StoreIndentItems != null && h.StoreIndentItems.Any(i => i.RaisedQuantity > i.ReceivedQuantity))
                .Count();

            // My drafts — filtered by IndentDate range
            int myDrafts = await _db.StoreIndents
                .Where(h =>
                    (h.Status == "Draft" || h.IndentType == "Draft Indent") &&
                    (string.IsNullOrEmpty(userName) || h.CreatedBy == userName) &&
                    (!plantId.HasValue || h.PlantId == plantId.Value) &&
                    (!fromDate.HasValue || h.IndentDate >= fromDate.Value) &&
                    (!toDate.HasValue || h.IndentDate <= toDate.Value))
                .CountAsync();

            // Near-expiry batches — from approved indents in date range, expiring within window
            int nearExpiryBatches = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                            x.Header.Status == "Approved" &&
                            (!fromDate.HasValue || x.Header.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || x.Header.IndentDate <= toDate.Value) &&
                            (!plantId.HasValue || x.Header.PlantId == plantId.Value))
                .CountAsync();

            // Expired batches — from approved indents in date range
            int expiredBatches = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate < today &&
                            x.Header.Status == "Approved" &&
                            (!fromDate.HasValue || x.Header.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || x.Header.IndentDate <= toDate.Value) &&
                            (!plantId.HasValue || x.Header.PlantId == plantId.Value))
                .CountAsync();

            // Central expired medicines (pending disposal) — Store only, ExpiryDate date-filtered (no repo changes)
            var pendingDisposalList = await _expiredRepo.ListPendingDisposalAsync(plantId);
            int expiredPendingDisposal = pendingDisposalList
                .Where(e => e.SourceType == "Store")
                .Where(e => !fromDate.HasValue || e.ExpiryDate >= fromDate.Value)
                .Where(e => !toDate.HasValue || e.ExpiryDate <= toDate.Value)
                .Count();

            // Low/Out-of-stock (group batches by MedItem within plant, date-filtered)
            var storeStocks = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Where(x => x.Header.Status == "Approved" &&
                            (!fromDate.HasValue || x.Header.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || x.Header.IndentDate <= toDate.Value))
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