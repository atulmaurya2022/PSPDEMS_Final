using EMS.WebApp.Configuration;
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EMS.WebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountLoginRepository _repo;
        private readonly ApplicationDbContext _db;
        private readonly SessionTimeoutOptions _timeoutOptions;

        public AccountController(IAccountLoginRepository repo, ApplicationDbContext db, IOptions<SessionTimeoutOptions> timeoutOptions)
        {
            _repo = repo;
            _db = db;
            _timeoutOptions = timeoutOptions.Value;
        }

        // GET: /Account/Login
        public async Task<IActionResult> Login()
       {
            //Always sign out any existing identity cookie — even if invalid
            await HttpContext.SignOutAsync();

            //Ensure TempData is not cleared by middleware accidentally
            Response.Cookies.Delete(".AspNetCore.Cookies");

            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string user_name, string password)
        {
            if (string.IsNullOrWhiteSpace(user_name) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Email and Password are required.";
                return View();
            }

            var user = await _repo.GetByEmailAndPasswordAsync(user_name, password);

            if (user == null)
            {
                ViewBag.Error = "Invalid credentials or user is inactive.";
                return View();
            }

            // If already logged in and user hasn't confirmed override
            if (!string.IsNullOrEmpty(user.SessionToken))
            {
                TempData["user_name"] = user_name;
                return RedirectToAction("ConfirmSessionOverride");
            }
            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ConfirmSessionOverride()
        {
            ViewBag.user_name = TempData["user_name"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProceedConfirmedLogin(string user_name, string password)
        {
            var user = await _repo.GetByEmailAndPasswordAsync(user_name, password);

            if (user == null)
            {
                ViewBag.Error = "Invalid credentials.";
                return RedirectToAction("Login");
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmSessionOverride(string user_name, string password)
        {
            return RedirectToAction("Login", new { user_name, confirm = true });
        }

        private async Task SignInUser(SysUser user)
        {
            // Generate a new session token
            var sessionToken = Guid.NewGuid().ToString();
            user.SessionToken = sessionToken;

            // Cross-platform UTC/IST TimeZone handling
            TimeZoneInfo istZone;
            try
            {
                istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch
            {
                try
                {
                    istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
                }
                catch
                {
                    istZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
                }
            }

            var nowUtc = DateTime.UtcNow;
            user.TokenIssuedAt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, istZone);
            user.LastActivityTime = nowUtc; // Set initial activity time in UTC

            // Save changes asynchronously
            await _repo.UpdateAsync(user);

            // Get user with role and plant information for claims
            var userWithDetails = await GetUserWithDetailsAsync(user.user_id);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.adid),
                new Claim("LoginId", user.user_id.ToString()),
                new Claim("FullName", user.full_name.ToString()),
                new Claim("LoginType", "local"),
                new Claim("SessionToken", sessionToken)
            };

            // Add role and plant information to claims
            if (userWithDetails?.SysRole != null)
            {
                claims.Add(new Claim("RoleName", userWithDetails.SysRole.role_name));
                claims.Add(new Claim("RoleId", userWithDetails.role_id.ToString()));
            }

            if (userWithDetails?.OrgPlant != null)
            {
                claims.Add(new Claim("PlantName", userWithDetails.OrgPlant.plant_name));
                claims.Add(new Claim("PlantId", userWithDetails.plant_id.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        private async Task<SysUser?> GetUserWithDetailsAsync(int userId)
        {
            return await _db.SysUsers
                .Include(u => u.SysRole)
                .Include(u => u.OrgPlant)
                .FirstOrDefaultAsync(u => u.user_id == userId);
        }
        //Autologin method for AD users
        [Authorize(AuthenticationSchemes = NegotiateDefaults.AuthenticationScheme)]
        public async Task<IActionResult> AutoLogin(bool confirm = false)
        {
            var adid = HttpContext.User.Identity?.Name;
            if (string.IsNullOrEmpty(adid))
            {
                TempData["Error"] = "Unable to retrieve AD identity.";
                return RedirectToAction("Login");
            }

            var usernameOnly = adid.Contains('\\') ? adid.Split('\\')[1] : adid;
            //var usernameOnly1 = adid.Contains('\\') ? adid.Split('\\')[1] : adid;
            //var usernameOnly = adid;

            var user = await _repo.GetByAdidAsync(usernameOnly);
            if (user == null)
            {
                TempData["Error"] = "User - "+ adid + " is not registered in EMS. Please contact Admin !";
                return RedirectToAction("Login");
            }

            if (!confirm && !string.IsNullOrEmpty(user.SessionToken))
            {
                int timeoutMinutes = _timeoutOptions.TimeoutMinutes;
                if(user.TokenIssuedAt.HasValue && (DateTime.UtcNow - user.TokenIssuedAt.Value).TotalMinutes >= timeoutMinutes)
                {
                    // Session has expired, clear the session token
                    user.SessionToken = null;
                    user.TokenIssuedAt = null;
                    await _repo.UpdateAsync(user);
                }
                else
                {
                    // Active session exists, prompt for confirmation
                    TempData["user_name"] = usernameOnly;
                    return RedirectToAction("ConfirmSessionOverrideAD");
                }
            }
            await SignInUser(user);
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmSessionOverrideAD()
        {
            ViewBag.adid = TempData["adid"];
            //return RedirectToAction("Doctor", "Dashboard");


            var userName = User.Identity?.Name;
            if (userName != null)
            {
                var user = await _repo.GetByEmailAsync(userName);
                if (user != null)
                {
                    user.SessionToken = null;
                    user.TokenIssuedAt = null;
                    await _repo.UpdateAsync(user);
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);



            return View();
        }

        [HttpPost]
        public IActionResult ConfirmSessionOverrideAD(string adid)
        {
            return RedirectToAction("AutoLogin", new { confirm = true });
        }




        // GET: /Account/Logout
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name;
            if (userName != null)
            {
                var user = await _repo.GetByEmailAsync(userName);
                if (user != null)
                {
                    user.SessionToken = null;
                    user.TokenIssuedAt = null;
                    await _repo.UpdateAsync(user);
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        public async Task<IActionResult> LogoutView(string reason = "")
        {




            var message = reason switch
            {
                "SessionTimeout" => "Your session has expired due to inactivity.",
                "SessionExpired" => "Your session has expired.",
                _ => "You have been logged out."
            };

            ViewBag.Message = message;
            ViewBag.Reason = reason;


            var userName = User.Identity?.Name;

            if (userName != null)
            {
                var user = await _repo.GetByEmailAsync(userName);
                if (user != null)
                {
                    user.SessionToken = null;
                    user.TokenIssuedAt = null;
                    await _repo.UpdateAsync(user);
                }


                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);


                return View();
            }
            else
            { 
                return RedirectToAction("AutoLogin", new { confirm = true });

            }
        }

        [HttpGet]
        public IActionResult GetTimeoutConfig()
        {
            return Json(new
            {
                timeoutMinutes = _timeoutOptions.TimeoutMinutes,
                warningMinutes = _timeoutOptions.WarningMinutes,
                checkIntervalSeconds = _timeoutOptions.CheckIntervalSeconds
            });
        }
       
    }
}