
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [AllowAnonymous] 
    public class UserInfoController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStoreIndentRepository _storeRepo;
        private readonly ICompounderIndentRepository _compounderRepo;

        public UserInfoController(
            ApplicationDbContext db,
            IStoreIndentRepository storeRepo,
            ICompounderIndentRepository compounderRepo)
        {
            _db = db;
            _storeRepo = storeRepo;
            _compounderRepo = compounderRepo;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Me()
        {
            try {
                var isAuth = User?.Identity?.IsAuthenticated == true;
                var key = (User?.Identity?.Name ?? string.Empty).Trim();

                // Defaults
                string fullName = string.Empty;
                string adid = string.Empty;
                string roleName = string.Empty;
                int? roleId = null;
                int? plantId = null;
                string plantName = string.Empty;

                if (isAuth && !string.IsNullOrWhiteSpace(key))
                {
                    var conn = _db.Database.GetDbConnection();
                    if (conn.State != ConnectionState.Open)
                        await conn.OpenAsync();

                    // 1) sys_user → full_name, adid, role_id, plant_id
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
SELECT TOP 1
    u.full_name,
    u.adid,
    u.role_id,
    u.plant_id
FROM dbo.sys_user u
WHERE
    LOWER(LTRIM(RTRIM(u.adid)))       = LOWER(LTRIM(RTRIM(@p0)))
";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@p0";
                        p.Value = key;
                        cmd.Parameters.Add(p);

                        await using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync())
                        {
                            fullName = r["full_name"] as string ?? string.Empty;
                            adid = r["adid"] as string ?? string.Empty;
                            if (r["role_id"] != DBNull.Value) roleId = Convert.ToInt32(r["role_id"]);
                            if (r["plant_id"] != DBNull.Value) plantId = Convert.ToInt32(r["plant_id"]);
                        }
                    }

                    // 2) sys.role / dbo.sys_role / dbo.role_master → role_name
                    if (roleId.HasValue)
                    {
                        roleName =
                            (await TryGetRoleNameAsync(conn, "sys", "role", roleId.Value)) ??
                            (await TryGetRoleNameAsync(conn, "dbo", "sys_role", roleId.Value)) ??
                            (await TryGetRoleNameAsync(conn, "dbo", "role_master", roleId.Value)) ??
                            string.Empty;
                    }

                    // 3) OrgPlants (existing DbSet) → plant_name
                    if (plantId.HasValue)
                    {
                        plantName = await _db.org_plants
                            .Where(p => p.plant_id == plantId.Value)
                            .Select(p => p.plant_name)
                            .FirstOrDefaultAsync() ?? string.Empty;
                    }
                }

                return Json(new
                {
                    isAuthenticated = isAuth,
                    userName = key,
                    fullName,
                    adid,
                    roleName,
                    plantId,
                    plantName
                });
            }
            catch (Exception Ex)
            {
                return Json(new
                {
                    isAuthenticated = "isAuth",
                    userName = "key",
                });
            }

        }

        private static async Task<string?> TryGetRoleNameAsync(DbConnection conn, string schema, string table, int roleId)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT TOP 1 role_name FROM [{schema}].[{table}] WHERE role_id = @rid";
                var p = cmd.CreateParameter();
                p.ParameterName = "@rid";
                p.Value = roleId;
                cmd.Parameters.Add(p);

                var obj = await cmd.ExecuteScalarAsync();
                var name = obj?.ToString()?.Trim();
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch
            {
                // Table or schema may not exist; swallow and return null to try next source
                return null;
            }
        }




    }


}
