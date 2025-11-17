using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize] 
    public class StoreIssueReportController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICompounderIndentRepository _compounderRepo;

        public StoreIssueReportController(
            ApplicationDbContext db,
            ICompounderIndentRepository compounderRepo)
        {
            _db = db;
            _compounderRepo = compounderRepo;
        }

        [HttpGet]
        public IActionResult Index() => View("StoreIssueReport");

        /// <summary>
        /// Details of medicines issued by Store to each Compounder in a date range.
        /// Date range applies to CompounderIndent.IndentDate (issue/fulfilment period).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReport(DateTime? fromDate, DateTime? toDate, int top = 5000)
        {
            var user = User?.Identity?.Name;
            int? plantId = null;
            if (!string.IsNullOrWhiteSpace(user))
                plantId = await _compounderRepo.GetUserPlantIdAsync(user);

            // Date range normalization (inclusive for days; we use < toExclusive)
            var today = DateTime.Today;
            var from = (fromDate?.Date) ?? new DateTime(today.Year, today.Month, 1);
            var toExclusive = (toDate?.Date.AddDays(1)) ?? from.AddMonths(1);

            var rows = await _db.CompounderIndentItems
                .Join(_db.CompounderIndents, i => i.IndentId, h => h.IndentId, (i, h) => new { i, h })
                .Join(_db.med_masters, ih => ih.i.MedItemId, m => m.MedItemId, (ih, m) => new { ih.i, ih.h, Med = m })
                .Where(x =>
                    x.h.IndentDate >= from && x.h.IndentDate < toExclusive &&
                    x.i.ReceivedQuantity > 0 &&
                    (!plantId.HasValue || x.h.plant_id == plantId.Value))
                .OrderByDescending(x => x.h.IndentDate)
                .Select(x => new StoreIssueRowDto
                {
                    IndentId = x.h.IndentId,
                    IndentDate = x.h.IndentDate,
                    Compounder = x.h.CreatedBy ?? string.Empty,
                    PlantName = x.h.OrgPlant != null ? x.h.OrgPlant.plant_name : string.Empty,
                    MedItemId = x.Med.MedItemId,
                    MedicineName = x.Med.MedItemName,
                    RaisedQty = x.i.RaisedQuantity,
                    IssuedQty = x.i.ReceivedQuantity,
                    BalanceQty = x.i.RaisedQuantity - x.i.ReceivedQuantity
                })
                .Take(top)
                .ToListAsync();

            return Json(rows);
        }

        [HttpGet]
        public async Task<IActionResult> Export(DateTime? fromDate, DateTime? toDate)
        {
            var result = await GetReport(fromDate, toDate, top: 100000) as JsonResult;
            var data = result?.Value as System.Collections.Generic.IEnumerable<StoreIssueRowDto>
                       ?? Array.Empty<StoreIssueRowDto>();

            var sb = new StringBuilder();
            sb.AppendLine("IndentId,IndentDate,Compounder,Plant,MedItemId,MedicineName,RaisedQty,IssuedQty,BalanceQty");
            foreach (var r in data.OrderBy(r => r.IndentDate).ThenBy(r => r.IndentId))
            {
                sb.Append(r.IndentId).Append(',')
                  .Append(r.IndentDate.ToString("yyyy-MM-dd")).Append(',')
                  .Append(Escape(r.Compounder)).Append(',')
                  .Append(Escape(r.PlantName)).Append(',')
                  .Append(r.MedItemId).Append(',')
                  .Append(Escape(r.MedicineName)).Append(',')
                  .Append(r.RaisedQty).Append(',')
                  .Append(r.IssuedQty).Append(',')
                  .Append(r.BalanceQty).AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var name = $"StoreIssue_{(fromDate?.ToString("yyyyMMdd") ?? "from")}_{(toDate?.ToString("yyyyMMdd") ?? "to")}.csv";
            return File(bytes, "text/csv", name);

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
