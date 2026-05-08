using System;
using System.Linq;
using System.Threading.Tasks;
using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public sealed class DashboardService : IDashboardService
    {
        private readonly IStoreIndentRepository _storeRepo;
        private readonly ICompounderIndentRepository _compounderRepo;
        private readonly IDoctorDiagnosisRepository _doctorRepo;
        private readonly IExpiredMedicineRepository _expiredRepo;
        private readonly ApplicationDbContext _db;

        public DashboardService(
            IStoreIndentRepository storeRepo,
            ICompounderIndentRepository compounderRepo,
            IDoctorDiagnosisRepository doctorRepo,
            IExpiredMedicineRepository expiredRepo,
            ApplicationDbContext db)
        {
            _storeRepo = storeRepo;
            _compounderRepo = compounderRepo;
            _doctorRepo = doctorRepo;
            _expiredRepo = expiredRepo;
            _db = db;
        }

        public async Task<DoctorDashboardDto> GetDoctorSummaryAsync(string? userName, string? user, int nearExpiryDays = 30, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Resolve user's plant for plant-wise visibility
            int? userPlantId = null;
            if (!string.IsNullOrWhiteSpace(user))
                userPlantId = await _storeRepo.GetUserPlantIdAsync(user);

            // Store pending — direct EF query with IndentDate filter
            int storePendingCount = await _db.StoreIndents
                .Where(h => h.Status == "Pending" &&
                            (!userPlantId.HasValue || h.PlantId == userPlantId.Value) &&
                            (!fromDate.HasValue || h.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || h.IndentDate <= toDate.Value))
                .CountAsync();

            // Compounder pending — direct EF query with IndentDate filter
            int compounderPendingCount = await _db.CompounderIndents
                .Where(h => h.Status == "Pending" &&
                            (!userPlantId.HasValue || h.plant_id == userPlantId.Value) &&
                            (!fromDate.HasValue || h.IndentDate >= fromDate.Value) &&
                            (!toDate.HasValue || h.IndentDate <= toDate.Value))
                .CountAsync();

            // Prescription pending — in-memory PrescriptionDate filter
            var prescList = await _doctorRepo.GetPendingApprovalsAsync(userPlantId);
            int prescPendingCount = prescList
                .Where(p => !fromDate.HasValue || p.PrescriptionDate >= fromDate.Value)
                .Where(p => !toDate.HasValue || p.PrescriptionDate <= toDate.Value)
                .Count();

            // Expired pending disposal — ExpiryDate filter, NO SourceType filter (doctors see Store + Compounder)
            var expiredPending = await _expiredRepo.ListPendingDisposalAsync(userPlantId);
            int expiredPendingCount = expiredPending
                .Where(e => !fromDate.HasValue || e.ExpiryDate >= fromDate.Value)
                .Where(e => !toDate.HasValue || e.ExpiryDate <= toDate.Value)
                .Count();

            // Near-expiry window
            var today = DateTime.Today;
            var upto = today.AddDays(nearExpiryDays);

            // Near-expiry from Compounder Inventory — ExpiryDate window + IndentDate range
            int compounderNearExpiryCount = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                    (!userPlantId.HasValue || x.Header.plant_id == userPlantId.Value) &&
                    (!fromDate.HasValue || x.Header.IndentDate >= fromDate.Value) &&
                    (!toDate.HasValue || x.Header.IndentDate <= toDate.Value))
                .CountAsync();

            // Near-expiry from Store Inventory — ExpiryDate window + IndentDate range
            int storeNearExpiryCount = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                    x.Header.Status == "Approved" &&
                    (!userPlantId.HasValue || x.Header.PlantId == userPlantId.Value) &&
                    (!fromDate.HasValue || x.Header.IndentDate >= fromDate.Value) &&
                    (!toDate.HasValue || x.Header.IndentDate <= toDate.Value))
                .CountAsync();

            return new DoctorDashboardDto
            {
                PendingStoreIndentApprovals = storePendingCount,
                PendingCompounderIndentApprovals = compounderPendingCount,
                PendingPrescriptionApprovals = prescPendingCount,
                ExpiredMedicinesPendingDisposal = expiredPendingCount,
                NearExpiryMedicineCount = compounderNearExpiryCount + storeNearExpiryCount,
                NearExpiryDays = nearExpiryDays
            };
        }
    }
}