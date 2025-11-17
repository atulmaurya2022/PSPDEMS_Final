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
    [Authorize("AccessSysRole")]
    public class SysRoleController : Controller
    {
        private readonly ISysRoleRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;
        public SysRoleController(ISysRoleRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListAsync();

                // Log data access for security monitoring
                await _auditService.LogAsync("sys_role", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} roles for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("sys_role", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load roles: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("sys_role", "CREATE_FORM_VIEW", "new", null, null,
                "Create role form accessed"));

            return PartialView("_CreateEdit", new SysRole());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SysRole model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("sys_role", "CREATE_ATTEMPT", recordId, null, model,
                    "Role creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("sys_role", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during role creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate role name
                if (await _repo.IsRoleNameExistsAsync(model.role_name))
                {
                    ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("sys_role", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate role: {model.role_name}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_role", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_sysrole_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_role", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 roles every 5 minutes. Please wait and try again.";
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

                // Save to database
                await _repo.AddAsync(model);
                recordId = model.role_id.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("sys_role", recordId, model,
                    $"Role '{model.role_name}' created successfully");

                return Json(new { success = true, message = "Role created successfully!", roleId = model.role_id });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("sys_role", "CREATE_FAILED", recordId, null, model,
                    $"Role creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_SysRole_RoleName_Unique") == true)
                {
                    ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the role. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    // Log not found attempt
                    await _auditService.LogAsync("sys_role", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent role with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("sys_role", id.ToString(),
                    $"Edit form accessed for role: {item.role_name}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_role", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SysRole model)
        {
            var recordId = model.role_id.ToString();
            SysRole? oldRole = null;

            try
            {
                // Get the current role for audit comparison
                oldRole = await _repo.GetByIdAsync(model.role_id);
                if (oldRole == null)
                {
                    await _auditService.LogAsync("sys_role", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent role");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("sys_role", "UPDATE_ATTEMPT", recordId, oldRole, model,
                    $"Role update attempt for: {oldRole.role_name}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("sys_role", "UPDATE_SECURITY_VIOLATION", recordId, oldRole, model,
                        "Insecure input detected during role update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate role name (excluding current record)
                if (await _repo.IsRoleNameExistsAsync(model.role_name, model.role_id))
                {
                    ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("sys_role", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldRole, model,
                        $"Attempted to update to duplicate role name: {model.role_name}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_role", "UPDATE_VALIDATION_FAILED", recordId, oldRole, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_sysrole_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_role", "UPDATE_RATE_LIMITED", recordId, oldRole, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 roles every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("sys_role", recordId, oldRole, model,
                    $"Role '{model.role_name}' updated successfully");

                return Json(new { success = true, message = "Role updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_role", "UPDATE_FAILED", recordId, oldRole, model,
                    $"Role update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_SysRole_RoleName_Unique") == true)
                {
                    ModelState.AddModelError("role_name", "A role with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the role. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        // FIXED Delete Method - NOW ACTUALLY DELETES THE RECORD
        [HttpPost]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            SysRole? roleToDelete = null;

            try
            {
                // Get entity before deletion for audit
                roleToDelete = await _repo.GetByIdAsync(id);
                if (roleToDelete == null)
                {
                    await _auditService.LogAsync("sys_role", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent role with ID: {id}");

                    return Json(new { success = false, message = "Role not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("sys_role", "DELETE_ATTEMPT", id.ToString(), roleToDelete, null,
                    $"Role deletion attempt for: {roleToDelete.role_name}");

                // ACTUALLY DELETE THE RECORD FROM DATABASE - THIS WAS MISSING!
                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("sys_role", id.ToString(), roleToDelete,
                    $"Role '{roleToDelete.role_name}' deleted successfully");

                return Json(new { success = true, message = "Role deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_role", "DELETE_FAILED", id.ToString(), roleToDelete, null,
                    $"Role deletion failed: {ex.Message}");

                // Check if the error is due to foreign key constraint
                if (ex.InnerException?.Message.Contains("REFERENCE constraint") == true ||
                    ex.InnerException?.Message.Contains("foreign key") == true)
                {
                    return Json(new { success = false, message = "Cannot delete this role as it is being used by other records." });
                }

                return Json(new { success = false, message = "Failed to delete role. Please try again." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("sys_role", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent role with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("sys_role", id.ToString(),
                    $"Role details viewed: {item.role_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_role", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading role details: {ex.Message}");

                return NotFound();
            }
        }

        // Add AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckRoleNameExists(string roleName, int? roleId = null)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return Json(new { exists = false });

            // Sanitize input before checking
            roleName = SanitizeString(roleName);

            var exists = await _repo.IsRoleNameExistsAsync(roleName, roleId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization

        private SysRole SanitizeInput(SysRole model)
        {
            if (model == null) return model;

            model.role_name = SanitizeString(model.role_name);
            model.role_desc = SanitizeString(model.role_desc);

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

        private bool IsInputSecure(SysRole model)
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

            var inputsToCheck = new[] { model.role_name, model.role_desc };

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