using Microsoft.AspNetCore.CookiePolicy;

namespace EMS.WebApp.Extensions
{
    public static class CookieSecurityExtensions
    {
        public static void ConfigureSecureCookies(this IServiceCollection services)
        {
            // Configure Cookie Policy
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.HttpOnly = HttpOnlyPolicy.Always;
                options.Secure = CookieSecurePolicy.Always;
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
            });

            // Configure Authentication Cookies (if using Identity)
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
                options.SlidingExpiration = true;
            });

            // Configure Session Cookies
            services.AddSession(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.IdleTimeout = TimeSpan.FromMinutes(20);
            });

            //services.AddAntiforgery(options =>
            //{
            //    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            //    options.Cookie.HttpOnly = true;                          // Recommended
            //    options.Cookie.SameSite = SameSiteMode.Strict;
            //});
        }
    }
}
