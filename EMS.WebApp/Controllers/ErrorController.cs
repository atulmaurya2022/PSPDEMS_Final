using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;

namespace EMS.WebApp.Controllers
{
    [AllowAnonymous] // Important: Allow access to error pages without authentication
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error/{statusCode?}")]
        public IActionResult HttpStatusCodeHandler(int? statusCode = null)
        {
            _logger.LogInformation("ErrorController.HttpStatusCodeHandler called with statusCode: {StatusCode}", statusCode);

            var statusCodeResult = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>();

            // Set default status code if not provided
            var actualStatusCode = statusCode ?? 500;

            ViewBag.StatusCode = actualStatusCode;
            ViewBag.Path = statusCodeResult?.OriginalPath ?? "Unknown";
            ViewBag.QS = statusCodeResult?.OriginalQueryString;

            // Set user-friendly error messages
            ViewBag.ErrorMessage = actualStatusCode switch
            {
                404 => "Sorry, the page you requested could not be found.",
                401 => "You are not authorized to access this resource.",
                403 => "You don't have permission to access this resource.",
                500 => "An internal server error occurred.",
                _ => "An error occurred while processing your request."
            };

            _logger.LogWarning("Displaying error page for status code {StatusCode}. Original path: {OriginalPath}",
                actualStatusCode, statusCodeResult?.OriginalPath);

            return View("Error");
        }

        [Route("Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            _logger.LogInformation("ErrorController.Error (general) called");

            var exceptionDetails = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

            if (exceptionDetails != null)
            {
                _logger.LogError(exceptionDetails.Error, "Unhandled exception in Error action");
                ViewBag.ErrorMessage = "An unexpected error occurred";
                ViewBag.Path = exceptionDetails.Path;
            }
            else
            {
                ViewBag.ErrorMessage = "An unknown error occurred";
                ViewBag.Path = "Unknown";
            }

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        // Test action to verify the controller is working
        [Route("Error/Test")]
        public IActionResult Test()
        {
            _logger.LogInformation("ErrorController.Test called - Controller is working!");
            return Json(new { message = "ErrorController is working!", timestamp = DateTime.Now });
        }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}