using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Models;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly IDashboardService _dashboardService;
        private readonly IStoreIndentRepository _storeRepo;
        private readonly ICompounderIndentRepository _compounderRepo;
        private readonly IDoctorDiagnosisRepository _doctorRepo;
        private readonly IExpiredMedicineRepository _expiredRepo;
        private readonly ApplicationDbContext _db;

        private readonly ISysUserRepository _repo;
        public DashboardController(
            ILogger<DashboardController> logger,
            IDashboardService dashboardService,
            IStoreIndentRepository storeRepo,
            ICompounderIndentRepository compounderRepo,
            IDoctorDiagnosisRepository doctorRepo,
            IExpiredMedicineRepository expiredRepo,
            ISysUserRepository repo,
            ApplicationDbContext db)
        {
            _logger = logger;
            _dashboardService = dashboardService;
            _storeRepo = storeRepo;
            _compounderRepo = compounderRepo;
            _doctorRepo = doctorRepo;
            _expiredRepo = expiredRepo;

            _repo = repo;
            _db = db;
        }

        public IActionResult Compounder()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public IActionResult Doctor()
        {
            return View();
        }
        public IActionResult Store()
        {
            return View();
        }

        public IActionResult Index()
        {
            var currentUserName = User.Identity?.Name;

            string ScreenName = "Compounder";
            var currentUserRole = User.FindFirst("RoleName")?.Value ?? "";

            if (currentUserRole.ToLower().Contains("store"))
            {
                ScreenName = "Store";
            }
            else if (currentUserRole.ToLower().Contains("compounder"))
            {
                ScreenName = "Compounder";
            }
            else if (currentUserRole.ToLower().Contains("doctor"))
            {
                ScreenName = "Doctor";
            }
            else if (currentUserRole.ToLower().Contains("admin"))
            {
                return RedirectToAction("Index", "AdminDashboard");
            }
            return View(ScreenName);

        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetDoctorSummary(int nearDays = 30, DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            var dto = await _dashboardService.GetDoctorSummaryAsync(
                User.Identity?.Name + " - " + User.GetFullName(),
                User.Identity?.Name,
                nearDays,
                fromDate,
                toDate);
            return Json(dto);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetNearExpiry(int days = 30, int top = 10, DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            int? userPlantId = null;
            var userName = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(userName))
                userPlantId = await _storeRepo.GetUserPlantIdAsync(userName);

            var today = DateTime.Today;
            var upto = today.AddDays(days);

            // Compounder Inventory — near-expiry within window, IndentDate in range
            var compounderRows = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                    (!userPlantId.HasValue || x.Header.plant_id == userPlantId.Value) &&
                    x.Header.IndentDate >= fromDate.Value &&
                    x.Header.IndentDate <= toDate.Value)
                .Select(x => new NearExpiryDto
                {
                    BatchId = x.b.BatchId,
                    MedicineName = x.Med.MedItemName,
                    BatchNo = x.b.BatchNo,
                    ExpiryDate = x.b.ExpiryDate,
                    AvailableStock = x.b.AvailableStock,
                    VendorCode = x.b.VendorCode,
                    Source = "Compounder"
                })
                .ToListAsync();

            // Store Inventory — near-expiry within window, IndentDate in range
            var storeRows = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate >= today && x.b.ExpiryDate <= upto &&
                    x.Header.Status == "Approved" &&
                    (!userPlantId.HasValue || x.Header.PlantId == userPlantId.Value) &&
                    x.Header.IndentDate >= fromDate.Value &&
                    x.Header.IndentDate <= toDate.Value)
                .Select(x => new NearExpiryDto
                {
                    BatchId = x.b.BatchId,
                    MedicineName = x.Med.MedItemName,
                    BatchNo = x.b.BatchNo,
                    ExpiryDate = x.b.ExpiryDate,
                    AvailableStock = x.b.AvailableStock,
                    VendorCode = x.b.VendorCode,
                    Source = "Store"
                })
                .ToListAsync();

            var combinedRows = compounderRows
                .Concat(storeRows)
                .OrderBy(x => x.ExpiryDate)
                .Take(top)
                .ToList();

            return Json(combinedRows);
        }

        //[HttpGet]
        //[Authorize]
        //public async Task<IActionResult> GetNearExpiry(int days = 30, int top = 10)
        //{
        //    // Resolve plant
        //    int? userPlantId = null;
        //    var userName = User.Identity?.Name;
        //    if (!string.IsNullOrWhiteSpace(userName))
        //    {
        //        userPlantId = await _storeRepo.GetUserPlantIdAsync(userName);
        //    }

        //    var today = DateTime.Today;
        //    var upto = today.AddDays(days);

        //    // Join: batches -> items -> header -> med master (for name)
        //    var rows = await _db.CompounderIndentBatches
        //        .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
        //        .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
        //        .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
        //        .Where(x =>
        //            x.b.AvailableStock > 0 &&
        //            x.b.ExpiryDate >= today &&
        //            x.b.ExpiryDate <= upto &&
        //            (!userPlantId.HasValue || x.Header.plant_id == userPlantId.Value))
        //        .OrderBy(x => x.b.ExpiryDate)
        //        .Select(x => new NearExpiryDto
        //        {
        //            BatchId = x.b.BatchId,
        //            MedicineName = x.Med.MedItemName,
        //            BatchNo = x.b.BatchNo,
        //            ExpiryDate = x.b.ExpiryDate,
        //            AvailableStock = x.b.AvailableStock,
        //            VendorCode = x.b.VendorCode
        //        })
        //        .Take(top)
        //        .ToListAsync();

        //    return Json(rows);
        //}

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetPendingDisposal(int top = 10, DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            int? userPlantId = null;
            var userName = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(userName))
                userPlantId = await _storeRepo.GetUserPlantIdAsync(userName);

            // Doctor sees both Store and Compounder disposal records — no SourceType filter
            var rows = await _expiredRepo.ListPendingDisposalAsync(userPlantId);

            var payload = rows
                .Where(e => e.ExpiryDate >= fromDate.Value && e.ExpiryDate <= toDate.Value)
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

            return Json(payload);
        }

        //[HttpGet]
        //[Authorize]
        //public async Task<IActionResult> GetExpired(int top = 10)
        //{
        //    // Resolve plant
        //    int? userPlantId = null;
        //    var userName = User.Identity?.Name;
        //    if (!string.IsNullOrWhiteSpace(userName))
        //    {
        //        userPlantId = await _storeRepo.GetUserPlantIdAsync(userName);
        //    }
        //    var today = DateTime.Today;

        //    var rows = await _db.CompounderIndentBatches
        //        .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
        //        .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
        //        .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
        //        .Where(x => x.b.AvailableStock > 0 &&
        //                    x.b.ExpiryDate < today &&
        //                    (!userPlantId.HasValue || x.Header.plant_id == userPlantId.Value))
        //        .OrderBy(x => x.b.ExpiryDate)
        //         .Select(x => new NearExpiryDto
        //         {
        //             BatchId = x.b.BatchId,
        //             MedicineName = x.Med.MedItemName,
        //             BatchNo = x.b.BatchNo,
        //             ExpiryDate = x.b.ExpiryDate,
        //             AvailableStock = x.b.AvailableStock,
        //             VendorCode = x.b.VendorCode
        //         })
        //        .Take(top)
        //        .ToListAsync();

        //    return Json(rows);
        //}
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetExpired(int top = 10, DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            int? userPlantId = null;
            var userName = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(userName))
                userPlantId = await _storeRepo.GetUserPlantIdAsync(userName);

            var today = DateTime.Today;

            // Compounder Inventory — expired, IndentDate in range
            var compounderRows = await _db.CompounderIndentBatches
                .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate < today &&
                    (!userPlantId.HasValue || x.Header.plant_id == userPlantId.Value) &&
                    x.Header.IndentDate >= fromDate.Value &&
                    x.Header.IndentDate <= toDate.Value)
                .Select(x => new NearExpiryDto
                {
                    BatchId = x.b.BatchId,
                    MedicineName = x.Med.MedItemName,
                    BatchNo = x.b.BatchNo,
                    ExpiryDate = x.b.ExpiryDate,
                    AvailableStock = x.b.AvailableStock,
                    VendorCode = x.b.VendorCode,
                    Source = "Compounder"
                })
                .ToListAsync();

            // Store Inventory — expired, IndentDate in range
            var storeRows = await _db.StoreIndentBatches
                .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                .Where(x =>
                    x.b.AvailableStock > 0 &&
                    x.b.ExpiryDate < today &&
                    x.Header.Status == "Approved" &&
                    (!userPlantId.HasValue || x.Header.PlantId == userPlantId.Value) &&
                    x.Header.IndentDate >= fromDate.Value &&
                    x.Header.IndentDate <= toDate.Value)
                .Select(x => new NearExpiryDto
                {
                    BatchId = x.b.BatchId,
                    MedicineName = x.Med.MedItemName,
                    BatchNo = x.b.BatchNo,
                    ExpiryDate = x.b.ExpiryDate,
                    AvailableStock = x.b.AvailableStock,
                    VendorCode = x.b.VendorCode,
                    Source = "Store"
                })
                .ToListAsync();

            var combinedRows = compounderRows
                .Concat(storeRows)
                .OrderBy(x => x.ExpiryDate)
                .Take(top)
                .ToList();

            return Json(combinedRows);
        }


        private async Task<int?> ResolveUserPlantIdAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return null;
            return await _storeRepo.GetUserPlantIdAsync(userName);
        }

        [HttpGet]
        public async Task<IActionResult> ListStoreIndentPending(DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            var plantId = await ResolveUserPlantIdAsync();

            var list = await _db.StoreIndents
                .Include(s => s.OrgPlant)
                .Where(s => s.Status == "Pending" &&
                            (!plantId.HasValue || s.PlantId == plantId.Value) &&
                            s.IndentDate >= fromDate.Value &&
                            s.IndentDate <= toDate.Value)
                .OrderByDescending(s => s.IndentDate)
                .ToListAsync();

            var payload = list.Select(s => new StoreIndentPendingDto
            {
                IndentId = s.IndentId,
                IndentType = s.IndentType ?? "",
                IndentDate = s.IndentDate,
                CreatedBy = s.CreatedBy ?? "",
                PlantName = s.OrgPlant?.plant_name ?? "",
                Status = s.Status ?? ""
            }).ToList();

            return Json(payload);
        }
        [HttpGet]
        public async Task<IActionResult> ListCompounderIndentPending(DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            var plantId = await ResolveUserPlantIdAsync();

            var list = await _db.CompounderIndents
                .Include(s => s.OrgPlant)
                .Where(s => s.Status == "Pending" &&
                            (!plantId.HasValue || s.plant_id == plantId.Value) &&
                            s.IndentDate >= fromDate.Value &&
                            s.IndentDate <= toDate.Value)
                .OrderByDescending(s => s.IndentDate)
                .ToListAsync();

            var payload = list.Select(s => new CompounderIndentPendingDto
            {
                IndentId = s.IndentId,
                IndentType = s.IndentType ?? "",
                IndentDate = s.IndentDate,
                CreatedBy = s.CreatedBy ?? "",
                PlantName = s.OrgPlant?.plant_name ?? "",
                Status = s.Status ?? ""
            }).ToList();

            return Json(payload);
        }

        [HttpGet]
        public async Task<IActionResult> ListPrescriptionPending(DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.Today.AddDays(-30);
            toDate ??= DateTime.Today;
            var plantId = await ResolveUserPlantIdAsync();
            var list = await _doctorRepo.GetPendingApprovalsAsync(plantId);

            var payload = list
                .Where(p => p.PrescriptionDate >= fromDate.Value && p.PrescriptionDate <= toDate.Value)
                .OrderByDescending(p => p.PrescriptionDate)
                .Select(p => new PrescriptionPendingDto
                {
                    PrescriptionId = p.PrescriptionId,
                    EmployeeId = p.EmployeeId,
                    EmployeeName = p.EmployeeName,
                    Department = p.Department,
                    Plant = p.Plant,
                    PrescriptionDate = p.PrescriptionDate,
                    VisitType = p.VisitType,
                    CreatedBy = p.CreatedBy,
                    MedicineCount = p.MedicineCount
                })
                .ToList();

            return Json(payload);
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}