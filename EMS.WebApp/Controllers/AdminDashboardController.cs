using EMS.WebApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminDashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index() => View("Index");

        // Aggregates for cards/lists (safe, no PII)
        [HttpGet]
        public async Task<IActionResult> Cards()
        {
            var now = DateTime.UtcNow;
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();

            // Users
            int totalUsers = await SafeScalarIntAsync(conn,
                "SELECT COUNT(*) FROM dbo.sys_user");

            // Admin wants inactivity info – support multiple possible column names for last login
            string? lastLoginCol = await PickExistingColumnAsync(conn, "dbo", "sys_user",
                new[] { "last_login_at", "last_login_on", "last_login", "last_signin_at", "last_signin" });

            int inactive30 = 0, neverLogged = 0;
            if (!string.IsNullOrEmpty(lastLoginCol))
            {
                inactive30 = await SafeScalarIntAsync(conn, $@"
                    SELECT COUNT(*) FROM dbo.sys_user
                    WHERE {lastLoginCol} IS NOT NULL AND {lastLoginCol} < DATEADD(day, -30, GETDATE())");

                neverLogged = await SafeScalarIntAsync(conn, $@"
                    SELECT COUNT(*) FROM dbo.sys_user
                    WHERE {lastLoginCol} IS NULL");
            }
            else
            {
                // No last-login column; treat as unknown (0s)
                inactive30 = 0; neverLogged = 0;
            }

            // Roles (try common role tables)
            int totalRoles = await FirstNonZeroAsync(conn, new[]
            {
                ("sys", "role"),
                ("dbo", "sys_role"),
                ("dbo", "role_master")
            });

            // Plants (we know OrgPlants exists in your context)
            int totalPlants = await _db.org_plants.CountAsync();

            // Errors/Audit (last 24h)
            var errors24 = await CountErrorsAsync(conn, hoursBack: 24);
            var errors1 = await CountErrorsAsync(conn, hoursBack: 1);
            var errors6 = await CountErrorsAsync(conn, hoursBack: 6);
            var topSources24 = await TopErrorSourcesAsync(conn, hoursBack: 24, top: 5);

            // Audit: recent errors/failures – return sanitized (timestamp + type + short msg)
            var recentAudit = await RecentAuditErrorsAsync(conn, hoursBack: 24, top: 10);

            // App version/build
            var asm = Assembly.GetExecutingAssembly();
            var version = asm.GetName().Version?.ToString() ?? "1.0.0.0";
            var buildDate = System.IO.File.GetLastWriteTimeUtc(asm.Location);

            return Json(new
            {
                users = new
                {
                    total = totalUsers,
                    inactive30,
                    neverLogged
                },
                inventory = new
                {
                    plants = totalPlants,
                    roles = totalRoles
                },
                errors = new
                {
                    last24h = errors24,
                    last6h = errors6,
                    last1h = errors1,
                    topSources24h = topSources24
                },
                audit = new
                {
                    recent = recentAudit
                },
                app = new
                {
                    version,
                    buildUtc = buildDate.ToString("yyyy-MM-dd HH:mm:ss") + "Z"
                }
            });
        }

        // ------------ Helpers (safe fallbacks, no crashes) ------------

        private static async Task<int> SafeScalarIntAsync(DbConnection conn, string sql)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var obj = await cmd.ExecuteScalarAsync();
                return (obj == null || obj == DBNull.Value) ? 0 : Convert.ToInt32(obj);
            }
            catch
            {
                return 0;
            }
        }

        private static async Task<string?> PickExistingColumnAsync(DbConnection conn, string schema, string table, IEnumerable<string> candidates)
        {
            try
            {
                foreach (var c in candidates)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t AND COLUMN_NAME = @c";
                    var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; ps.Value = schema;
                    var pt = cmd.CreateParameter(); pt.ParameterName = "@t"; pt.Value = table;
                    var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; pc.Value = c;
                    cmd.Parameters.Add(ps); cmd.Parameters.Add(pt); cmd.Parameters.Add(pc);

                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (count > 0) return c;
                }
            }
            catch { }
            return null;
        }

        private static async Task<int> FirstNonZeroAsync(DbConnection conn, (string schema, string table)[] tables)
        {
            foreach (var t in tables)
            {
                var sql = $"SELECT COUNT(*) FROM [{t.schema}].[{t.table}]";
                var n = await SafeScalarIntAsync(conn, sql);
                if (n > 0) return n;
            }
            return 0;
        }

        private static async Task<int> CountErrorsAsync(DbConnection conn, int hoursBack)
        {
            // Try common log tables & columns
            var candidates = new (string schema, string table, string tsCol, string levelCol)[]
            {
                ("dbo","app_log","created_at","level"),
                ("dbo","logs","logged_at","level"),
                ("dbo","error_log","created_at","level"),
                ("dbo","sys_log","created_at","log_level")
            };

            foreach (var c in candidates)
            {
                if (!await TableExistsAsync(conn, c.schema, c.table)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.tsCol)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.levelCol)) continue;

                var sql = $@"
                SELECT COUNT(*) FROM [{c.schema}].[{c.table}]
                WHERE {c.tsCol} >= DATEADD(hour, -@h, GETDATE())
                AND UPPER({c.levelCol}) IN ('ERROR','CRITICAL')";
                try
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var p = cmd.CreateParameter(); p.ParameterName = "@h"; p.Value = hoursBack; cmd.Parameters.Add(p);
                    var o = await cmd.ExecuteScalarAsync();
                    return (o == null || o == DBNull.Value) ? 0 : Convert.ToInt32(o);
                }
                catch { continue; }
            }
            return 0;
        }

        private static async Task<List<object>> TopErrorSourcesAsync(DbConnection conn, int hoursBack, int top)
        {
            var candidates = new (string schema, string table, string tsCol, string levelCol, string sourceCol)[]
            {
                ("dbo","app_log","created_at","level","source"),
                ("dbo","logs","logged_at","level","logger"),
                ("dbo","error_log","created_at","level","source"),
                ("dbo","sys_log","created_at","log_level","logger")
            };

            foreach (var c in candidates)
            {
                if (!await TableExistsAsync(conn, c.schema, c.table)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.tsCol)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.levelCol)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.sourceCol)) continue;

                var sql = $@"
SELECT TOP (@top)
    ISNULL(CAST({c.sourceCol} AS NVARCHAR(200)), 'unknown') AS src,
    COUNT(*) AS cnt
FROM [{c.schema}].[{c.table}]
WHERE {c.tsCol} >= DATEADD(hour, -@h, GETDATE())
  AND UPPER({c.levelCol}) IN ('ERROR','CRITICAL')
GROUP BY {c.sourceCol}
ORDER BY cnt DESC";
                try
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@h"; p1.Value = hoursBack; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@top"; p2.Value = top; cmd.Parameters.Add(p2);

                    var list = new List<object>();
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var src = r["src"]?.ToString() ?? "unknown";
                        var cnt = Convert.ToInt32(r["cnt"]);
                        list.Add(new { source = src, count = cnt });
                    }
                    return list;
                }
                catch { continue; }
            }
            return new List<object>();
        }

        private static async Task<List<object>> RecentAuditErrorsAsync(DbConnection conn, int hoursBack, int top)
        {
            // tuple type: last element is a plain string that may be null
            var candidates = new (string schema, string table, string tsCol, string levelCol, string msgCol, string typeCol)[]
            {
        ("dbo", "audit_log", "created_at", "level", "message", "category"),
        ("dbo", "sys_audit", "created_at", "level", "message", "event_type"),
        ("dbo", "app_log",  "created_at", "level", "message", null)
            };

            foreach (var c in candidates)
            {
                if (!await TableExistsAsync(conn, c.schema, c.table)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.tsCol)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.levelCol)) continue;
                if (!await ColumnExistsAsync(conn, c.schema, c.table, c.msgCol)) continue;

                var levelExpr = $"UPPER({c.levelCol}) IN ('ERROR','CRITICAL','FAIL','FAILED')";
                var typeExpr = c.typeCol != null ? c.typeCol : "NULL"; // inline column or SQL NULL

                var sql = $@"
SELECT TOP (@top)
    {c.tsCol} AS ts,
    {typeExpr} AS typ,
    {c.msgCol} AS msg
FROM [{c.schema}].[{c.table}]
WHERE {c.tsCol} >= DATEADD(hour, -@h, GETDATE())
  AND {levelExpr}
ORDER BY {c.tsCol} DESC";

                try
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@h"; p1.Value = hoursBack; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@top"; p2.Value = top; cmd.Parameters.Add(p2);

                    var list = new List<object>();
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        var ts = r["ts"]?.ToString() ?? "";
                        var typ = r["typ"]?.ToString() ?? "";
                        var msg = (r["msg"]?.ToString() ?? "").Trim();
                        if (msg.Length > 180) msg = msg.Substring(0, 177) + "...";
                        list.Add(new { timestamp = ts, type = typ, message = msg });
                    }
                    return list;
                }
                catch
                {
                    // try next candidate table
                    continue;
                }
            }

            return new List<object>();
        }




        private static async Task<bool> TableExistsAsync(DbConnection conn, string schema, string table)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t";
                var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; ps.Value = schema;
                var pt = cmd.CreateParameter(); pt.ParameterName = "@t"; pt.Value = table;
                cmd.Parameters.Add(ps); cmd.Parameters.Add(pt);
                var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return n > 0;
            }
            catch { return false; }
        }

        private static async Task<bool> ColumnExistsAsync(DbConnection conn, string schema, string table, string column)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t AND COLUMN_NAME = @c";
                var ps = cmd.CreateParameter(); ps.ParameterName = "@s"; ps.Value = schema;
                var pt = cmd.CreateParameter(); pt.ParameterName = "@t"; pt.Value = table;
                var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; pc.Value = column;
                cmd.Parameters.Add(ps); cmd.Parameters.Add(pt); cmd.Parameters.Add(pc);
                var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return n > 0;
            }
            catch { return false; }
        }
    }
}
