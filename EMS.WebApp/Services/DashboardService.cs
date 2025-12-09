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

        public async Task<DoctorDashboardDto> GetDoctorSummaryAsync(string? userName, string? user, int nearExpiryDays = 30)
        {
            // Resolve user's plant for plant-wise visibility
            int? userPlantId = null;
            if (!string.IsNullOrWhiteSpace(user))
            {
                userPlantId = await _storeRepo.GetUserPlantIdAsync(user);
            }

            // Approvals (status = "Pending")
            var storePending = await _storeRepo.ListByStatusAsync("Pending", currentUser: null, userPlantId: userPlantId);
            var compounderPending = await _compounderRepo.ListByStatusAsync("Pending", currentUser: null, userPlantId: userPlantId);
            var prescPending = await _doctorRepo.GetPendingApprovalCountAsync(userPlantId);

            // Expired (pending disposal)
            var expiredPending = await _expiredRepo.ListPendingDisposalAsync(userPlantId);

            // Near-expiry calculation
            var today = DateTime.Today;
            var upto = today.AddDays(nearExpiryDays);

            // Near-expiry count from Compounder Inventory
            var compounderNearExpiryCount = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate >= today &&
                    x.b.ExpiryDate <= upto &&
                    (!userPlantId.HasValue || x.Header.plant_id == userPlantId.Value))
                .CountAsync();

            // Near-expiry count from Store Inventory
            var storeNearExpiryCount = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, Header = h })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate >= today &&
                    x.b.ExpiryDate <= upto &&
                    x.Header.Status == "Approved" &&
                    (!userPlantId.HasValue || x.Header.PlantId == userPlantId.Value))
                .CountAsync();

            // Total near-expiry count (Store + Compounder)
            var totalNearExpiryCount = compounderNearExpiryCount + storeNearExpiryCount;

            return new DoctorDashboardDto
            {
                PendingStoreIndentApprovals = storePending.Count(),
                PendingCompounderIndentApprovals = compounderPending.Count(),
                PendingPrescriptionApprovals = prescPending,
                ExpiredMedicinesPendingDisposal = expiredPending.Count(),
                NearExpiryMedicineCount = totalNearExpiryCount,
                NearExpiryDays = nearExpiryDays
            };
        }
    }
}