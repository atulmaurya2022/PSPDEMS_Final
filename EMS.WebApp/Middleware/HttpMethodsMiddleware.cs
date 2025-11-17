namespace EMS.WebApp.Middleware
{
    public class HttpMethodsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HashSet<string> _allowedMethods;

        public HttpMethodsMiddleware(RequestDelegate next)
        {
            _next = next;
            _allowedMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
        };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_allowedMethods.Contains(context.Request.Method))
            {
                context.Response.StatusCode = 405; // Method Not Allowed
                await context.Response.WriteAsync("Method Not Allowed");
                return;
            }

            await _next(context);
        }
    }
}
