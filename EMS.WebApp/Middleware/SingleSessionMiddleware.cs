using EMS.WebApp.Configuration;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EMS.WebApp.Middleware
{
    public class SingleSessionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SessionTimeoutOptions _timeoutOptions;

        public SingleSessionMiddleware(RequestDelegate next, IOptions<SessionTimeoutOptions> timeoutOptions)
        {
            _next = next;
            _timeoutOptions = timeoutOptions.Value;
        }

        public async Task InvokeAsync(HttpContext context, IAccountLoginRepository repo)
        {
            var path = context.Request.Path.Value?.ToLower();

            // ✅ Skip validation for these safe paths to prevent redirect loops
            if (path != null && (
                path.Contains("/account/login") ||
                path.Contains("/account/logout") ||
                path.Contains("/account/logoutview") ||
                path.Contains("/account/confirmsessionoverride") ||
                path.Contains("/account/proceedconfirmedlogin") ||
                path.Contains("/session/check") ||
                path.Contains("/session/heartbeat") ||
                path.Contains("/session/timeout") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/images/") ||
                path.StartsWith("/lib/")
            ))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userName = context.User.Identity.Name;
                var sessionTokenClaim = context.User.FindFirst("SessionToken")?.Value;

                if (userName != null && sessionTokenClaim != null)
                {
                    var user = await repo.GetByEmailAsync(userName);
                    Console.WriteLine($"[Middleware] User: {userName}, Claim Token: {sessionTokenClaim}, DB Token: {user?.SessionToken}");

                    if (user?.SessionToken != sessionTokenClaim)
                    {
                        Console.WriteLine("[Middleware] Token mismatch — logging out");
                        await LogoutUser(context, "SessionExpired");
                        return;
                    }

                    // Check for session timeout
                    if (user?.LastActivityTime.HasValue == true)
                    {
                        var timeSinceLastActivity = DateTime.UtcNow - user.LastActivityTime.Value;
                        if (timeSinceLastActivity > _timeoutOptions.TimeoutDuration)
                        {
                            Console.WriteLine($"[Middleware] Session timeout — user inactive for {timeSinceLastActivity.TotalMinutes:F1} minutes");

                            // Clear session token from database
                            user.SessionToken = null;
                            user.TokenIssuedAt = null;
                            user.LastActivityTime = null;
                            await repo.UpdateAsync(user);

                            await LogoutUser(context, "SessionTimeout");
                            return;
                        }
                    }

                    // Update last activity time for non-heartbeat requests
                    if (!path.Contains("/session/heartbeat"))
                    {
                        user.LastActivityTime = DateTime.UtcNow;
                        await repo.UpdateAsync(user);
                    }
                }
            }

            await _next(context);
        }

        private async Task LogoutUser(HttpContext context, string reason)
        {
            await context.SignOutAsync();

            var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         context.Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                context.Response.StatusCode = 401;
                context.Response.Headers["X-Session-Expired"] = "true";
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = reason == "SessionTimeout" ? "Your session has expired due to inactivity." : "Your session has expired.",
                    redirect = "/Account/LogoutView?reason=" + reason
                }));
            }
            else
            {
                context.Response.Redirect("/Account/LogoutView?reason=" + reason);
            }
        }
    }
}