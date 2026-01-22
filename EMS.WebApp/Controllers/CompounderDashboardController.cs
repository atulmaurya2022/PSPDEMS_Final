using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class CompounderDashboardController : Controller
    {
        private readonly ILogger<CompounderDashboardController> _logger;
        private readonly ICompounderDashboardService _svc;
        private readonly ICompounderIndentRepository _compounderRepo;
        private readonly IExpiredMedicineRepository _expiredRepo;
        private readonly ApplicationDbContext _db;

        public CompounderDashboardController(
            ILogger<CompounderDashboardController> logger,
            ICompounderDashboardService svc,
            ICompounderIndentRepository compounderRepo,
            IExpiredMedicineRepository expiredRepo,
            ApplicationDbContext db)
        {
            _logger = logger;
            _svc = svc;
            _compounderRepo = compounderRepo;
            _expiredRepo = expiredRepo;
            _db = db;
        }

        /// <summary>
        /// Gets the plant ID for the current user.
        /// </summary>
        private async Task<int?> ResolvePlantAsync()
        {
            var user = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(user)) return null;
            return await _compounderRepo.GetUserPlantIdAsync(user);
        }

        /// <summary>
        /// Gets the full username in format "loginName - FullName".
        /// </summary>
        private string GetFullUserName()
        {
            return User.Identity?.Name + " - " + User.GetFullName();
        }

        /// <summary>
        /// Checks if the current user's plant is BCM.
        /// </summary>
        private async Task<bool> IsBcmPlantAsync(int? plantId)
        {
            return await _svc.IsBcmPlantAsync(plantId);
        }

        /// <summary>
        /// Gets the current user's role.
        /// </summary>
        private async Task<string?> GetUserRoleAsync()
        {
            var user = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(user)) return null;
            return await _svc.GetUserRoleAsync(user);
        }

        [HttpGet]
        public IActionResult Index() => View("Compounder");

        /// <summary>
        /// Gets the current user's plant information for dynamic label display.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPlantInfo()
        {
            try
            {
                var plantId = await ResolvePlantAsync();
                var plantName = await GetCurrentUserPlantNameAsync();
                var plantCode = plantId.HasValue ? await _svc.GetPlantCodeAsync(plantId) : null;

                return Json(new
                {
                    success = true,
                    plantId = plantId,
                    plantName = plantName ?? string.Empty,
                    plantCode = plantCode ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting plant info");
                return Json(new { success = false, plantName = string.Empty, plantCode = string.Empty });
            }
        }

        /// <summary>
        /// Gets the current user's plant name.
        /// </summary>
        private async Task<string?> GetCurrentUserPlantNameAsync()
        {
            var user = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(user)) return null;

            var userEntity = await _db.SysUsers
                .Include(u => u.OrgPlant)
                .FirstOrDefaultAsync(u => u.full_name == user || u.email == user || u.adid == user);

            return userEntity?.OrgPlant?.plant_name;
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary(int nearDays = 30)
        {
            var dto = await _svc.GetSummaryAsync(GetFullUserName(), User.Identity?.Name, nearDays);
            return Json(dto);
        }

        // --- Drill-downs ---

        [HttpGet]
        public async Task<IActionResult> ListPending()
        {
            var plant = await ResolvePlantAsync();
            var isBcm = await IsBcmPlantAsync(plant);
            var currentUser = isBcm ? GetFullUserName() : null;

            var list = await _compounderRepo.ListByStatusAsync("Pending", currentUser: currentUser, userPlantId: plant);

            // Apply BCM user filter - only show records created by current user
            if (isBcm && !string.IsNullOrEmpty(currentUser))
            {
                list = list.Where(h => h.CreatedBy == currentUser);
            }

            var payload = list
                .OrderByDescending(h => h.IndentDate)
                .Select(h => new CompounderIndentPendingDto
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
            var isBcm = await IsBcmPlantAsync(plant);
            var currentUser = isBcm ? GetFullUserName() : null;

            var list = await _compounderRepo.ListByStatusAsync("Approved", currentUser: currentUser, userPlantId: plant);

            // Apply BCM user filter - only show records created by current user
            if (isBcm && !string.IsNullOrEmpty(currentUser))
            {
                list = list.Where(h => h.CreatedBy == currentUser);
            }

            var payload = list
                .Where(h => h.CompounderIndentItems != null && h.CompounderIndentItems.Any(i => i.RaisedQuantity > i.ReceivedQuantity))
                .OrderBy(h => h.IndentDate)
                .Select(h => new CompounderIndentPendingDto
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
            var user = GetFullUserName();

            // My Drafts is always filtered by current user regardless of plant
            var rows = await _db.CompounderIndents
                .Where(h =>
                    (h.Status == "Draft" || h.IndentType == "Draft Indent") &&
                    h.CreatedBy == user &&
                    (!plant.HasValue || h.plant_id == plant.Value))
                .OrderByDescending(h => h.IndentDate)
                .Select(h => new CompounderIndentPendingDto
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
            var isBcm = await IsBcmPlantAsync(plant);
            var currentUser = isBcm ? GetFullUserName() : null;

            var today = DateTime.Today;
            var upto = today.AddDays(days);

            var query = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                            (!plant.HasValue || x.Header.plant_id == plant.Value));

            // Apply BCM user filter
            if (isBcm && !string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(x => x.Header.CreatedBy == currentUser);
            }

            var rows = await query
                .OrderBy(x => x.b.ExpiryDate)
                .Select(x => new NearExpiryDto
                {
                    BatchId = x.b.BatchId,
                    MedicineName = x.Med.MedItemName,
                    BatchNo = x.b.BatchNo,
                    ExpiryDate = x.b.ExpiryDate,
                    AvailableStock = x.b.AvailableStock,
                    VendorCode = x.b.VendorCode ?? string.Empty
                })
                .Take(top)
                .ToListAsync();

            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingDisposal(int top = 100)
        {
            var plant = await ResolvePlantAsync();
            var isBcm = await IsBcmPlantAsync(plant);
            var userRole = await GetUserRoleAsync();
            var currentUser = isBcm ? GetFullUserName() : null;

            // Pass userRole AND currentUser for BCM filtering
            // Repository signature: ListPendingDisposalAsync(int? userPlantId, string? userRole, string? currentUser)
            var rows = (await _expiredRepo.ListPendingDisposalAsync(plant, userRole, currentUser))
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
            var isBcm = await IsBcmPlantAsync(plant);
            var currentUser = isBcm ? GetFullUserName() : null;

            var today = DateTime.Today;

            var query = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x => x.b.AvailableStock > 0 &&
                            x.b.ExpiryDate < today &&
                            (!plant.HasValue || x.Header.plant_id == plant.Value));

            // Apply BCM user filter
            if (isBcm && !string.IsNullOrEmpty(currentUser))
            {
                query = query.Where(x => x.Header.CreatedBy == currentUser);
            }

            var rows = await query
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
            var isBcm = await IsBcmPlantAsync(plant);
            var currentUser = isBcm ? GetFullUserName() : null;

            var stocksQuery = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Where(x => !plant.HasValue || x.Header.plant_id == plant.Value);

            // Apply BCM user filter
            if (isBcm && !string.IsNullOrEmpty(currentUser))
            {
                stocksQuery = stocksQuery.Where(x => x.Header.CreatedBy == currentUser);
            }

            var stocks = await stocksQuery
                .GroupBy(x => new { x.i.MedItemId, x.Header.plant_id })
                .Select(g => new
                {
                    g.Key.MedItemId,
                    PlantId = g.Key.plant_id,
                    Total = g.Sum(z => z.b.AvailableStock)
                })
                .ToListAsync();

            var meds = await _db.med_masters
                .Select(m => new { m.MedItemId, m.MedItemName, ReorderLevel = (int?)m.ReorderLimit })
                .ToListAsync();

            var rows = stocks
                .Select(s =>
                {
                    var m = meds.FirstOrDefault(mm => mm.MedItemId == s.MedItemId);
                    var reorder = m?.ReorderLevel;
                    int threshold = reorder.HasValue ? reorder.Value : fallback;

                    return new CompounderLowStockDto
                    {
                        MedItemId = s.MedItemId,
                        MedicineName = m?.MedItemName ?? ("Med#" + s.MedItemId),
                        TotalAvailable = s.Total,
                        ReorderLevel = reorder
                    };
                })
                .Where(x => x.TotalAvailable > 0 &&
                            x.TotalAvailable <= (x.ReorderLevel.HasValue ? x.ReorderLevel.Value : fallback))
                .OrderBy(x => x.TotalAvailable)
                .Take(top)
                .ToList();

            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> GetOutOfStock(int top = 100)
        {
            var plant = await ResolvePlantAsync();
            var isBcm = await IsBcmPlantAsync(plant);
            var currentUser = isBcm ? GetFullUserName() : null;

            var stocksQuery = _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Where(x => !plant.HasValue || x.Header.plant_id == plant.Value);

            // Apply BCM user filter
            if (isBcm && !string.IsNullOrEmpty(currentUser))
            {
                stocksQuery = stocksQuery.Where(x => x.Header.CreatedBy == currentUser);
            }

            var stocks = await stocksQuery
                .GroupBy(x => new { x.i.MedItemId, x.Header.plant_id })
                .Select(g => new
                {
                    g.Key.MedItemId,
                    PlantId = g.Key.plant_id,
                    Total = g.Sum(z => z.b.AvailableStock)
                })
                .ToListAsync();

            var meds = await _db.med_masters
                .Select(m => new { m.MedItemId, m.MedItemName })
                .ToListAsync();

            var rows = stocks
                .Where(s => s.Total <= 0)
                .OrderBy(s => s.MedItemId)
                .Take(top)
                .Select(s => new CompounderLowStockDto
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