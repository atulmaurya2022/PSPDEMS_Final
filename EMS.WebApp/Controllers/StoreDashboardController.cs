

using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class StoreDashboardController : Controller
    {
        private readonly ILogger<StoreDashboardController> _logger;
        private readonly IStoreDashboardService _svc;
        private readonly IStoreIndentRepository _storeRepo;
        private readonly IExpiredMedicineRepository _expiredRepo;
        private readonly ApplicationDbContext _db;

        public StoreDashboardController(
            ILogger<StoreDashboardController> logger,
            IStoreDashboardService svc,
            IStoreIndentRepository storeRepo,
            IExpiredMedicineRepository expiredRepo,
            ApplicationDbContext db)
        {
            _logger = logger;
            _svc = svc;
            _storeRepo = storeRepo;
            _expiredRepo = expiredRepo;
            _db = db;
        }

        private async Task<int?> ResolvePlantAsync()
        {
            var user = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(user)) return null;
            return await _storeRepo.GetUserPlantIdAsync(user);
        }

        [HttpGet]
        public IActionResult Index() => View("Store");

        [HttpGet]
        public async Task<IActionResult> GetSummary(int nearDays = 30)
        {
            var dto = await _svc.GetSummaryAsync(User.Identity?.Name + " - " + User.GetFullName()/*User?.Identity?.Name*/, User.Identity?.Name, nearDays);
            return Json(dto);
        }

        // --- Drill-downs ---

        [HttpGet]
        public async Task<IActionResult> ListPending()
        {
            var plant = await ResolvePlantAsync();
            var list = await _storeRepo.ListByStatusAsync("Pending", currentUser: null, userPlantId: plant);

            var payload = list
                .OrderByDescending(h => h.IndentDate)
                .Select(h => new StoreIndentPendingDto
                {
                    IndentId = h.IndentId,
                    IndentType = h.IndentType ?? "",
                    IndentDate = h.IndentDate,
                    CreatedBy = h.CreatedBy ?? "",
                    PlantName = h.OrgPlant?.plant_name ?? "",
                    Status = h.Status ?? ""
                })
                .ToList();

            return Json(payload);
        }

        [HttpGet]
        public async Task<IActionResult> ListApprovedAwaitingReceipt()
        {
            var plant = await ResolvePlantAsync();
            var list = await _storeRepo.ListByStatusAsync("Approved", currentUser: null, userPlantId: plant);

            var payload = list
                .Where(h => h.StoreIndentItems != null && h.StoreIndentItems.Any(i => i.RaisedQuantity > i.ReceivedQuantity))
                .OrderBy(h => h.IndentDate)
                .Select(h => new StoreIndentPendingDto
                {
                    IndentId = h.IndentId,
                    IndentType = h.IndentType ?? "",
                    IndentDate = h.IndentDate,
                    CreatedBy = h.CreatedBy ?? "",
                    PlantName = h.OrgPlant?.plant_name ?? "",
                    Status = h.Status ?? ""
                })
                .ToList();

            return Json(payload);
        }

        [HttpGet]
        public async Task<IActionResult> ListMyDrafts()
        {
            var plant = await ResolvePlantAsync();
            var user = User.Identity?.Name + " - " + User.GetFullName() ?? "";
            var rows = await _db.StoreIndents
                .Where(h =>
                    (h.Status == "Draft" || h.IndentType == "Draft Indent") &&
                    h.CreatedBy == user &&
                    (!plant.HasValue || h.PlantId == plant.Value))
                .OrderByDescending(h => h.IndentDate)
                .Select(h => new StoreIndentPendingDto
                {
                    IndentId = h.IndentId,
                    IndentType = h.IndentType ?? "",
                    IndentDate = h.IndentDate,
                    CreatedBy = h.CreatedBy ?? "",
                    PlantName = h.OrgPlant != null ? h.OrgPlant.plant_name : "",
                    Status = h.Status ?? ""
                })
                .ToListAsync();

            return Json(rows);
        }



        [HttpGet]
        public async Task<IActionResult> GetNearExpiry(int days = 30, int top = 100)
        {
            var plant = await ResolvePlantAsync();
            var today = DateTime.Today; var upto = today.AddDays(days);

            var rows = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                            (!plant.HasValue || x.Header.PlantId == plant.Value))
                .OrderBy(x => x.b.ExpiryDate)
                .Select(x => new NearExpiryDto
                {
                    BatchId = x.b.BatchId,
                    MedicineName = x.Med.MedItemName,
                    BatchNo = x.b.BatchNo,
                    ExpiryDate = x.b.ExpiryDate,
                    AvailableStock = x.b.AvailableStock,
                    VendorCode = x.b.VendorCode
                })
                .Take(top)
                .ToListAsync();

            return Json(rows);
        }



        [HttpGet]
        public async Task<IActionResult> GetPendingDisposal(int top = 100)
        {
            var plant = await ResolvePlantAsync();
            var rows = (await _expiredRepo.ListPendingDisposalAsync(plant))
                .OrderByDescending(e => e.ExpiryDate)
                .Take(top)
                .Select(e => new ExpiredMedicineDto
                {
                    Id = e.ExpiredMedicineId,
                    MedicineName = e.MedicineName,
                    BatchNo = e.BatchNumber ?? "",
                    TypeOfMedicine = e.TypeOfMedicine ?? "",
                    Status = e.Status ?? "",
                    ExpiredOn = e.ExpiryDate
                })
                .ToList();

            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> GetExpired(int top = 100)
        {
            var plant = await ResolvePlantAsync();
            var today = DateTime.Today; 

            var rows = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate < today &&
                            (!plant.HasValue || x.Header.PlantId == plant.Value))
                .OrderBy(x => x.b.ExpiryDate)
                 .Select(x => new NearExpiryDto
                 {
                     BatchId = x.b.BatchId,
                     MedicineName = x.Med.MedItemName,
                     BatchNo = x.b.BatchNo,
                     ExpiryDate = x.b.ExpiryDate,
                     AvailableStock = x.b.AvailableStock,
                     VendorCode = x.b.VendorCode
                 })
                .Take(top)
                .ToListAsync();

            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> GetLowStock(int fallback = 10, int top = 100)
        {
            var plant = await ResolvePlantAsync();

            var stocks = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .GroupBy(x => new { x.i.MedItemId, x.Header.PlantId })
                .Select(g => new { g.Key.MedItemId, PlantId = g.Key.PlantId, Total = g.Sum(z => (int?)z.b.AvailableStock) ?? 0 })
                .Where(s => !plant.HasValue || s.PlantId == plant.Value)
                .ToListAsync();

            var meds = await _db.med_masters
                .Select(m => new { m.MedItemId, m.MedItemName, ReorderLevel = (int?)m.ReorderLimit })
                .ToListAsync();

            var rows = stocks
                .Select(s =>
                {
                    var m = meds.FirstOrDefault(mm => mm.MedItemId == s.MedItemId);
                    int threshold = m?.ReorderLevel ?? fallback;
                    return new StoreLowStockDto
                    {
                        MedItemId = s.MedItemId,
                        MedicineName = m?.MedItemName ?? ("Med#" + s.MedItemId),
                        TotalAvailable = s.Total,
                        ReorderLevel = m?.ReorderLevel
                    };
                })
                .Where(x => x.TotalAvailable > 0 && x.TotalAvailable <= (x.ReorderLevel ?? fallback))
                .OrderBy(x => x.TotalAvailable)
                .Take(top)
                .ToList();

            return Json(rows);
        }


        [HttpGet]
        public async Task<IActionResult> GetOutOfStock(int top = 100)
        {
            var plant = await ResolvePlantAsync();

            var stocks = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .GroupBy(x => new { x.i.MedItemId, x.Header.PlantId })
                .Select(g => new { g.Key.MedItemId, PlantId = g.Key.PlantId, Total = g.Sum(z => (int?)z.b.AvailableStock) ?? 0 })
                .Where(s => !plant.HasValue || s.PlantId == plant.Value)
                .ToListAsync();

            var meds = await _db.med_masters
                .Select(m => new { m.MedItemId, m.MedItemName })
                .ToListAsync();

            var rows = stocks
                .Where(s => s.Total <= 0)
                .OrderBy(s => s.MedItemId)
                .Take(top)
                .Select(s => new StoreLowStockDto
                {
                    MedItemId = s.MedItemId,
                    MedicineName = meds.FirstOrDefault(mm => mm.MedItemId == s.MedItemId)?.MedItemName ?? ("Med#" + s.MedItemId),
                    TotalAvailable = s.Total,
                    ReorderLevel = null
                })
                .ToList();

            return Json(rows);
        }


    }
}
