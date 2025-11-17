
using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize] // keep generic; plug your policy if you have one
    public class NearExpiryReportController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStoreIndentRepository _storeRepo;
        private readonly ICompounderIndentRepository _compounderRepo;

        public NearExpiryReportController(
            ApplicationDbContext db,
            IStoreIndentRepository storeRepo,
            ICompounderIndentRepository compounderRepo)
        {
            _db = db;
            _storeRepo = storeRepo;
            _compounderRepo = compounderRepo;
        }

        [HttpGet]
        public IActionResult Index() => View("NearExpiryReport");

        // scope: Store | Compounder | Both
        [HttpGet]
        public async Task<IActionResult> GetReport(DateTime? pivot, int months = 1, string scope = "Both", int top = 500)
        {
            var user = User.Identity?.Name + " - " + User.GetFullName();
            int? plantStore = null, plantComp = null, plantId = null;
            if (!string.IsNullOrWhiteSpace(user))
            {
                plantStore = await _storeRepo.GetUserPlantIdAsync(user);
                plantComp = await _compounderRepo.GetUserPlantIdAsync(user);
                plantId = plantStore ?? plantComp;
            }

            var pivotDate = (pivot ?? DateTime.Today).Date;
            months = months < 1 ? 1 : (months > 3 ? 3 : months);
            var endDate = pivotDate.AddMonths(months);

            var rows = new System.Collections.Generic.List<NearExpiryReportRowDto>();

            if (!string.Equals(scope, "Compounder", StringComparison.OrdinalIgnoreCase))
            {
                // STORE scope
                var storeRows = await _db.StoreIndentBatches
                    .Join(_db.StoreIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                    .Join(_db.StoreIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                    .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                    .Where(x => x.b.AvailableStock > 0
                                && x.b.ExpiryDate > pivotDate && x.b.ExpiryDate <= endDate
                                && (!plantId.HasValue || x.Header.PlantId == plantId.Value))
                    .OrderBy(x => x.b.ExpiryDate)
                    .Select(x => new NearExpiryReportRowDto
                    {
                        Scope = "Store",
                        BatchId = x.b.BatchId,
                        MedItemId = x.Med.MedItemId,
                        MedicineName = x.Med.MedItemName,
                        BatchNo = x.b.BatchNo,
                        ExpiryDate = x.b.ExpiryDate,
                        DaysFromPivot = EF.Functions.DateDiffDay(pivotDate, x.b.ExpiryDate),
                        AvailableStock = x.b.AvailableStock,
                        VendorCode = x.b.VendorCode ?? string.Empty
                    })
                    .Take(top)
                    .ToListAsync();

                rows.AddRange(storeRows);
            }

            if (!string.Equals(scope, "Store", StringComparison.OrdinalIgnoreCase))
            {
                // COMPOUNDER scope
                var compRows = await _db.CompounderIndentBatches
                    .Join(_db.CompounderIndentItems, b => b.IndentItemId, i => i.IndentItemId, (b, i) => new { b, i })
                    .Join(_db.CompounderIndents, bi => bi.i.IndentId, h => h.IndentId, (bi, h) => new { bi.b, bi.i, Header = h })
                    .Join(_db.med_masters, bih => bih.i.MedItemId, m => m.MedItemId, (bih, m) => new { bih.b, bih.Header, Med = m })
                    .Where(x => x.b.AvailableStock > 0
                                && x.b.ExpiryDate > pivotDate && x.b.ExpiryDate <= endDate
                                && (!plantId.HasValue || x.Header.plant_id == plantId.Value))
                    .OrderBy(x => x.b.ExpiryDate)
                    .Select(x => new NearExpiryReportRowDto
                    {
                        Scope = "Compounder",
                        BatchId = x.b.BatchId,
                        MedItemId = x.Med.MedItemId,
                        MedicineName = x.Med.MedItemName,
                        BatchNo = x.b.BatchNo,
                        ExpiryDate = x.b.ExpiryDate,
                        DaysFromPivot = EF.Functions.DateDiffDay(pivotDate, x.b.ExpiryDate),
                        AvailableStock = x.b.AvailableStock,
                        VendorCode = x.b.VendorCode ?? string.Empty
                    })
                    .Take(top)
                    .ToListAsync();

                rows.AddRange(compRows);
            }

            // consistent sort
            rows = rows.OrderBy(r => r.ExpiryDate).ThenBy(r => r.MedicineName).ToList();

            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Export(DateTime? pivot, int months = 1, string scope = "Both")
        {
            var result = await GetReport(pivot, months, scope, top: 5000) as JsonResult;
            var data = result?.Value as System.Collections.Generic.IEnumerable<NearExpiryReportRowDto>
                       ?? Array.Empty<NearExpiryReportRowDto>();

            var sb = new StringBuilder();
            sb.AppendLine("Scope,MedItemId,MedicineName,BatchNo,ExpiryDate,DaysFromPivot,AvailableStock,VendorCode,BatchId");
            foreach (var r in data)
            {
                sb.Append(Escape(r.Scope)).Append(',')
                  .Append(r.MedItemId).Append(',')
                  .Append(Escape(r.MedicineName)).Append(',')
                  .Append(Escape(r.BatchNo)).Append(',')
                  .Append(r.ExpiryDate.ToString("yyyy-MM-dd")).Append(',')
                  .Append(r.DaysFromPivot).Append(',')
                  .Append(r.AvailableStock).Append(',')
                  .Append(Escape(r.VendorCode)).Append(',')
                  .Append(r.BatchId).AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"NearExpiry_{(pivot ?? DateTime.Today):yyyyMMdd}_M{(months < 1 ? 1 : (months > 3 ? 3 : months))}_{scope}.csv";
            return File(bytes, "text/csv", fileName);

            static string Escape(string s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                    return "\"" + s.Replace("\"", "\"\"") + "\"";
                return s;
            }
        }
    }
}
