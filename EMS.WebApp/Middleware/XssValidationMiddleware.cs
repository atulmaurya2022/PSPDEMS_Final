using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;

namespace EMS.WebApp.Middleware
{
    public class XssValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public XssValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            HttpRequest request = context.Request;

            // Endpoint routing (ASP.NET Core 3.0+)
            var endpoint = context.GetEndpoint();
            var actionDesc = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var controllerName = actionDesc?.ControllerName;

            string excludedController = "EmailConfig"; // Change this to your actual controller name

            if (!string.IsNullOrEmpty(controllerName) &&
                string.Equals(controllerName, excludedController, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Iterate over all form fields
            //"Perform validation only if it's NOT the excluded controller


            // Validate Query Parameters
            foreach (var kvp in context.Request.Query)
            {
                foreach (var value in kvp.Value)
                {
                    if (!IsInputValid(value))
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("Invalid input detected (query).");
                        return;
                    }
                }
            }

            // Validate Form Parameters
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                foreach (var kvp in form)
                {
                    foreach (var value in kvp.Value)
                    {
                        if (!IsInputValid(value))
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsync("Invalid input detected (form).");
                            return;
                        }
                    }
                }
            }

            await _next(context);
        }

        private static readonly System.Text.RegularExpressions.Regex XssRegex = new System.Text.RegularExpressions.Regex(
            @"(<script[^>]*>[\s\S]*?</script>|%3Cscript[^>]*%3E[\s\S]*?%3C/script%3E|javascript:|vbscript:|onload\s*=|onerror\s*=|onclick\s*=|onmouseover\s*=|onfocus\s*=|onblur\s*=|<iframe|%3Ciframe)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private bool IsInputValid(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return true;

            return !XssRegex.IsMatch(input);
        }
    }


}
