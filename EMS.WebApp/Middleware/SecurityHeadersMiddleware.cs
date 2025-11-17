namespace EMS.WebApp.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Remove revealing headers
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // Remove server information
                headers.Remove("Server");
                headers.Remove("X-Powered-By");
                headers.Remove("X-AspNet-Version");
                headers.Remove("X-AspNetMvc-Version");

                // Security headers
                headers.Append("X-Content-Type-Options", "nosniff");
                headers.Append("X-Frame-Options", "SAMEORIGIN");
                headers.Append("X-XSS-Protection", "1; mode=block");
                headers.Append("Referrer-Policy", "strict-origin");
                headers.Append("X-Permitted-Cross-Domain-Policies", "none");
                headers.Append("Permissions-Policy",
                    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

                // Content Security Policy
                headers.Append("Content-Security-Policy",
                    "default-src 'self'; font-src 'self'; frame-ancestors 'self'; object-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';");

                // Cache control for sensitive pages
                if (context.Request.Path.StartsWithSegments("/Account") ||
                    context.Request.Path.StartsWithSegments("/Admin"))
                {
                    headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    headers.Append("Pragma", "no-cache");
                    headers.Append("Expires", "0");
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
