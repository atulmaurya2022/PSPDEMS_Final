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
    [Authorize("AccessDepartmentMaster")]
    public class DepartmentMasterController : Controller
    {
        private readonly IDepartmentMasterRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public DepartmentMasterController(IDepartmentMasterRepository repo, IMemoryCache cache, IAuditService auditService)
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
                await _auditService.LogAsync("org_department", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} department records for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("org_department", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load department records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("org_department", "CREATE_FORM_VIEW", "new", null, null,
                "Create department record form accessed"));

            return PartialView("_CreateEdit", new OrgDepartment());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrgDepartment model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("org_department", "CREATE_ATTEMPT", recordId, null, model,
                    "Department record creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("org_department", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during department record creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate department name
                if (await _repo.IsDepartmentNameExistsAsync(model.dept_name))
                {
                    ModelState.AddModelError("dept_name", "A department with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("org_department", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate department: {model.dept_name}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("org_department", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_department_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("org_department", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 departments every 5 minutes. Please wait and try again.";
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
                recordId = model.dept_id.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("org_department", recordId, model,
                    $"Department '{model.dept_name}' created successfully");

                return Json(new { success = true, message = "Department created successfully!", deptId = model.dept_id });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("org_department", "CREATE_FAILED", recordId, null, model,
                    $"Department record creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_OrgDepartment_DeptName_Unique") == true)
                {
                    ModelState.AddModelError("dept_name", "A department with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the department. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(short id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    // Log not found attempt
                    await _auditService.LogAsync("org_department", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent department record with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("org_department", id.ToString(),
                    $"Edit form accessed for department: {item.dept_name}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("org_department", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OrgDepartment model)
        {
            var recordId = model.dept_id.ToString();
            OrgDepartment? oldDepartment = null;

            try
            {
                // Get the current department for audit comparison
                oldDepartment = await _repo.GetByIdAsync(model.dept_id);
                if (oldDepartment == null)
                {
                    await _auditService.LogAsync("org_department", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent department record");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("org_department", "UPDATE_ATTEMPT", recordId, oldDepartment, model,
                    $"Department record update attempt for: {oldDepartment.dept_name}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("org_department", "UPDATE_SECURITY_VIOLATION", recordId, oldDepartment, model,
                        "Insecure input detected during department record update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate department name (excluding current record)
                if (await _repo.IsDepartmentNameExistsAsync(model.dept_name, model.dept_id))
                {
                    ModelState.AddModelError("dept_name", "A department with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("org_department", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldDepartment, model,
                        $"Attempted to update to duplicate department: {model.dept_name}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("org_department", "UPDATE_VALIDATION_FAILED", recordId, oldDepartment, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_department_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("org_department", "UPDATE_RATE_LIMITED", recordId, oldDepartment, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 departments every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("org_department", recordId, oldDepartment, model,
                    $"Department '{model.dept_name}' updated successfully");

                return Json(new { success = true, message = "Department updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("org_department", "UPDATE_FAILED", recordId, oldDepartment, model,
                    $"Department record update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_OrgDepartment_DeptName_Unique") == true)
                {
                    ModelState.AddModelError("dept_name", "A department with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the department. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(short id)
        {
            OrgDepartment? departmentToDelete = null;

            try
            {
                // Get entity before deletion for audit
                departmentToDelete = await _repo.GetByIdAsync(id);
                if (departmentToDelete == null)
                {
                    await _auditService.LogAsync("org_department", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent department record with ID: {id}");

                    return Json(new { success = false, message = "Department record not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("org_department", "DELETE_ATTEMPT", id.ToString(), departmentToDelete, null,
                    $"Department record deletion attempt for: {departmentToDelete.dept_name}");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("org_department", id.ToString(), departmentToDelete,
                    $"Department '{departmentToDelete.dept_name}' deleted successfully");

                return Json(new { success = true, message = "Department deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("org_department", "DELETE_FAILED", id.ToString(), departmentToDelete, null,
                    $"Department record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the department." });
            }
        }

        public async Task<IActionResult> Details(short id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("org_department", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent department record with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("org_department", id.ToString(),
                    $"Department record details viewed: {item.dept_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("org_department", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading department record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckDepartmentNameExists(string deptName, short? deptId = null)
        {
            if (string.IsNullOrWhiteSpace(deptName))
                return Json(new { exists = false });

            // Sanitize input before checking
            deptName = SanitizeString(deptName);

            var exists = await _repo.IsDepartmentNameExistsAsync(deptName, deptId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private OrgDepartment SanitizeInput(OrgDepartment model)
        {
            if (model == null) return model;

            model.dept_name = SanitizeString(model.dept_name);
            model.dept_description = SanitizeString(model.dept_description);
            model.Remarks = SanitizeString(model.Remarks);

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

        private bool IsInputSecure(OrgDepartment model)
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

            var inputsToCheck = new[] { model.dept_name, model.dept_description, model.Remarks };

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

        #endregion

        private string GetCurrentUserName()
        {
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
            var utcNow = DateTime.UtcNow;
            var istTimeZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, istTimeZone);
        }
    }
}