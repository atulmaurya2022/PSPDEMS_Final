using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessSystemScreenMaster")]
    public class SystemScreenMasterController : Controller
    {
        private readonly ISystemScreenMasterRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public SystemScreenMasterController(ISystemScreenMasterRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        // GET: /SystemScreenMaster
        public IActionResult Index() => View();

        // AJAX for DataTable
        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListAsync();

                // Log data access for security monitoring
                await _auditService.LogAsync("sys_screen_name", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} screen masters for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("sys_screen_name", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load screen masters: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        // GET: create form partial
        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("sys_screen_name", "CREATE_FORM_VIEW", "new", null, null,
                "Create screen master form accessed"));

            return PartialView("_CreateEdit", new SysScreenName());
        }

        // POST: create with enhanced security
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SysScreenName model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("sys_screen_name", "CREATE_ATTEMPT", recordId, null, model,
                    "Screen master creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("sys_screen_name", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during screen master creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate screen name
                if (await IsScreenNameExistsAsync(model.screen_name))
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("sys_screen_name", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate screen name: {model.screen_name}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_screen_name", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_systemscreenmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_screen_name", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 screen masters every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Set audit fields for creation
                var currentUser = GetCurrentUserName();
                var istDateTime = GetISTDateTime();

                model.CreatedBy = currentUser;
                model.CreatedOn = istDateTime;
                model.ModifiedBy = currentUser;
                model.ModifiedOn = istDateTime;

                // Check if controller exists
                var result = await _repo.AddIfControllerExistsAsync(model);
                if (!result.Success)
                {
                    var controllerList = string.Join(", ", result.AvailableControllers);
                    ModelState.AddModelError("screen_name", $"No Screen with this name exists. Available Screen: {controllerList}");

                    // Log controller not found
                    await _auditService.LogAsync("sys_screen_name", "CREATE_CONTROLLER_NOT_FOUND", recordId, null, model,
                        $"Controller not found for screen: {model.screen_name}. Available controllers: {controllerList}");

                    return PartialView("_CreateEdit", model);
                }

                recordId = model.screen_uid.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("sys_screen_name", recordId, model,
                    $"Screen master '{model.screen_name}' created successfully");

                return Json(new { success = true, message = "Screen master created successfully!", screenUid = model.screen_uid });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("sys_screen_name", "CREATE_FAILED", recordId, null, model,
                    $"Screen master creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("screen_name") == true)
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the screen master. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        // GET: edit form partial
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    // Log not found attempt
                    await _auditService.LogAsync("sys_screen_name", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent screen master with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("sys_screen_name", id.ToString(),
                    $"Edit form accessed for screen master: {item.screen_name}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_screen_name", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        // POST: edit with enhanced security
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SysScreenName model)
        {
            var recordId = model.screen_uid.ToString();
            SysScreenName? oldScreenMaster = null;

            try
            {
                // Get the current screen master for audit comparison
                oldScreenMaster = await _repo.GetByIdAsync(model.screen_uid);
                if (oldScreenMaster == null)
                {
                    await _auditService.LogAsync("sys_screen_name", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent screen master");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("sys_screen_name", "UPDATE_ATTEMPT", recordId, oldScreenMaster, model,
                    $"Screen master update attempt for: {oldScreenMaster.screen_name}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("sys_screen_name", "UPDATE_SECURITY_VIOLATION", recordId, oldScreenMaster, model,
                        "Insecure input detected during screen master update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate screen name (excluding current record)
                if (await IsScreenNameExistsAsync(model.screen_name, model.screen_uid))
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("sys_screen_name", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldScreenMaster, model,
                        $"Attempted to update to duplicate screen name: {model.screen_name}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_screen_name", "UPDATE_VALIDATION_FAILED", recordId, oldScreenMaster, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_systemscreenmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_screen_name", "UPDATE_RATE_LIMITED", recordId, oldScreenMaster, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 screen masters every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Check if controller exists
                var result = await _repo.UpdateIfControllerExistsAsync(model, GetCurrentUserName(), GetISTDateTime());
                if (!result.Success)
                {
                    var controllerList = string.Join(", ", result.AvailableControllers);
                    ModelState.AddModelError("screen_name", $"No controller with this name exists. Available controllers: {controllerList}");

                    // Log controller not found
                    await _auditService.LogAsync("sys_screen_name", "UPDATE_CONTROLLER_NOT_FOUND", recordId, oldScreenMaster, model,
                        $"Controller not found for screen: {model.screen_name}. Available controllers: {controllerList}");

                    return PartialView("_CreateEdit", model);
                }

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("sys_screen_name", recordId, oldScreenMaster, model,
                    $"Screen master '{model.screen_name}' updated successfully");

                return Json(new { success = true, message = "Screen master updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_screen_name", "UPDATE_FAILED", recordId, oldScreenMaster, model,
                    $"Screen master update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("screen_name") == true)
                {
                    ModelState.AddModelError("screen_name", "A screen with this name already exists.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the screen master. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        // POST: delete
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            SysScreenName? screenMasterToDelete = null;

            try
            {
                // Get entity before deletion for audit
                screenMasterToDelete = await _repo.GetByIdAsync(id);
                if (screenMasterToDelete == null)
                {
                    await _auditService.LogAsync("sys_screen_name", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent screen master with ID: {id}");

                    return Json(new { success = false, message = "Screen master not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("sys_screen_name", "DELETE_ATTEMPT", id.ToString(), screenMasterToDelete, null,
                    $"Screen master deletion attempt for: {screenMasterToDelete.screen_name}");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("sys_screen_name", id.ToString(), screenMasterToDelete,
                    $"Screen master '{screenMasterToDelete.screen_name}' deleted successfully");

                return Json(new { success = true, message = "Screen master deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_screen_name", "DELETE_FAILED", id.ToString(), screenMasterToDelete, null,
                    $"Screen master deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the screen master." });
            }
        }

        // GET: /SystemScreenMaster/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("sys_screen_name", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent screen master with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("sys_screen_name", id.ToString(),
                    $"Screen master details viewed: {item.screen_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_screen_name", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading screen master details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckScreenNameExists(string screenName, int? screenUid = null)
        {
            if (string.IsNullOrWhiteSpace(screenName))
                return Json(new { exists = false });

            // Sanitize input before checking
            screenName = SanitizeString(screenName);

            var exists = await IsScreenNameExistsAsync(screenName, screenUid);
            return Json(new { exists = exists });
        }

        // Get available controllers for dropdown/autocomplete
        [HttpGet]
        public async Task<IActionResult> GetAvailableControllers()
        {
            try
            {
                // Log controller list access
                await _auditService.LogAsync("sys_screen_name", "CONTROLLER_LIST_ACCESS", "multiple", null, null,
                    "Available controllers list accessed");

                var controllers = GetAvailableControllerNames();
                return Json(new { controllers = controllers });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_screen_name", "CONTROLLER_LIST_ERROR", "multiple", null, null,
                    $"Error loading available controllers: {ex.Message}");

                return Json(new { controllers = new List<string>() });
            }
        }

        #region Private Methods for Input Sanitization and Validation

        private SysScreenName SanitizeInput(SysScreenName model)
        {
            if (model == null) return model;

            model.screen_name = SanitizeString(model.screen_name);
            model.screen_description = SanitizeString(model.screen_description);

            return model;
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // HTML encode the input to prevent XSS
            input = HttpUtility.HtmlEncode(input);

            // Remove or replace potentially dangerous characters
            input = Regex.Replace(input, @"[<>""'&]", "", RegexOptions.IgnoreCase);

            // Remove script tags and javascript
            input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"vbscript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

            return input.Trim();
        }

        private bool IsInputSecure(SysScreenName model)
        {
            if (model == null) return false;

            // Check for potentially dangerous patterns
            var dangerousPatterns = new[]
            {
                @"<script",
                @"</script>",
                @"javascript:",
                @"vbscript:",
                @"on\w+\s*=",
                @"eval\s*\(",
                @"expression\s*\(",
                @"<iframe",
                @"<object",
                @"<embed",
                @"<form",
                @"<input"
            };

            var inputsToCheck = new[] { model.screen_name, model.screen_description };

            foreach (var input in inputsToCheck)
            {
                if (string.IsNullOrEmpty(input)) continue;

                foreach (var pattern in dangerousPatterns)
                {
                    if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private async Task<bool> IsScreenNameExistsAsync(string screenName, int? excludeScreenUid = null)
        {
            try
            {
                var screens = await _repo.ListAsync();
                var query = screens.Where(s => s.screen_name.ToLower() == screenName.ToLower());

                if (excludeScreenUid.HasValue)
                {
                    query = query.Where(s => s.screen_uid != excludeScreenUid.Value);
                }

                return query.Any();
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetAvailableControllerNames()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
                    .Select(t => t.Name.Replace("Controller", ""))
                    .OrderBy(name => name)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private string GetCurrentUserName()
        {
            // Try to get user name from different claims
            var userName = User.Identity?.Name + " - " + User.GetFullName()
                          ?? User.FindFirst("name")?.Value
                          ?? User.FindFirst("user_name")?.Value
                          ?? User.FindFirst("email")?.Value
                          ?? User.FindFirst("user_id")?.Value
                          ?? "System";

            return userName;
        }

        private DateTime GetISTDateTime()
        {
            // Convert UTC to IST (UTC+5:30)
            var utcNow = DateTime.UtcNow;
            var istTimeZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, istTimeZone);
        }

        #endregion
    }
}