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
        private readonly SessionTimeoutOptions _timeoutOptions;

        public AccountController(IAccountLoginRepository repo, IOptions<SessionTimeoutOptions> timeoutOptions)
        {
            _repo = repo;
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
            //return null;
        }



        // POST: /Account/Login
        [HttpPost]
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
                TempData["password"] = password;
                return RedirectToAction("ConfirmSessionOverride");
            }
            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public IActionResult ConfirmSessionOverride()
        {
            ViewBag.user_name = TempData["user_name"];
            ViewBag.password = TempData["password"];
            return View();
        }

        [HttpPost]
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
        public IActionResult ConfirmSessionOverride(string user_name, string password)
        {
            return RedirectToAction("Login", new { user_name, password, confirm = true });
        }

        private async Task SignInUser(SysUser user)
        {
            // Generate a new session token
            var sessionToken = Guid.NewGuid().ToString();
            user.SessionToken = sessionToken;

            // Convert current UTC time to IST (Indian Standard Time)
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            user.TokenIssuedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            user.LastActivityTime = DateTime.UtcNow; // Set initial activity time

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

        // Add this helper method to AccountController
        private async Task<SysUser?> GetUserWithDetailsAsync(int userId)
        {
            // You'll need to inject ISysUserRepository into AccountController
            // Add this to AccountController constructor: ISysUserRepository sysUserRepo
            // private readonly ISysUserRepository _sysUserRepo;

            // For now, assuming you have access to ApplicationDbContext
            // If not, you can inject ISysUserRepository instead

            // This is a temporary implementation - adjust based on your DI setup
            using var scope = HttpContext.RequestServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.SysUsers
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