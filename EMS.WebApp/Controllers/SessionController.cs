using EMS.WebApp.Configuration;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EMS.WebApp.Controllers
{
    [Route("session")]
    public class SessionController : Controller
    {
        private readonly IAccountLoginRepository _repo;
        private readonly SessionTimeoutOptions _timeoutOptions;

        public SessionController(IAccountLoginRepository repo, IOptions<SessionTimeoutOptions> timeoutOptions)
        {
            _repo = repo;
            _timeoutOptions = timeoutOptions.Value;
        }

        [Authorize]
        [HttpGet("check")]
        public async Task<IActionResult> Check()
        {
            var userName = User.Identity?.Name;
            if (userName != null)
            {
                var user = await _repo.GetByEmailAsync(userName);
                if (user?.LastActivityTime.HasValue == true)
                {
                    var timeSinceLastActivity = DateTime.UtcNow - user.LastActivityTime.Value;
                    var remainingTime = _timeoutOptions.TimeoutDuration - timeSinceLastActivity;

                    return Json(new
                    {
                        isValid = remainingTime > TimeSpan.Zero,
                        remainingMinutes = Math.Max(0, remainingTime.TotalMinutes),
                        warningThreshold = _timeoutOptions.WarningDuration.TotalMinutes
                    });
                }
            }

            return Json(new { isValid = false, remainingMinutes = 0 });
        }

        [Authorize]
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat()
        {
            var userName = User.Identity?.Name;
            if (userName != null)
            {
                await _repo.UpdateLastActivityAsync(userName);
                return Json(new { success = true, timestamp = DateTime.UtcNow });
            }

            return Json(new { success = false });
        }

        [Authorize]
        [HttpGet("timeout")]
        public IActionResult Timeout()
        {
            return RedirectToAction("LogoutView", "Account", new { reason = "SessionTimeout" });
        }
    }
}