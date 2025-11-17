using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;
using EMS.WebApp.Extensions;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedCategory")]
    public class MedCategoryController : Controller
    {
        private readonly IMedCategoryRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedCategoryController(IMedCategoryRepository repo, IMemoryCache cache, IAuditService auditService)
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
                await _auditService.LogAsync("med_category", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} medical category records for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("med_category", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load medical category records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("med_category", "CREATE_FORM_VIEW", "new", null, null,
                "Create medical category record form accessed"));

            return PartialView("_CreateEdit", new MedCategory());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedCategory model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("med_category", "CREATE_ATTEMPT", recordId, null, model,
                    "Medical category record creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_category", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during medical category record creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate category name
                if (await _repo.IsCategoryNameExistsAsync(model.MedCatName))
                {
                    ModelState.AddModelError("MedCatName", "A medical category with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_category", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate medical category: {model.MedCatName}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_category", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medcategory_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_category", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 MedCategory records every 5 minutes. Please wait and try again.";
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
                recordId = model.MedCatId.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("med_category", recordId, model,
                    $"Medical category '{model.MedCatName}' created successfully");

                return Json(new { success = true, message = "Medical category created successfully!", medCatId = model.MedCatId });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("med_category", "CREATE_FAILED", recordId, null, model,
                    $"Medical category record creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedCategory_MedCatName_Unique") == true)
                {
                    ModelState.AddModelError("MedCatName", "A medical category with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the medical category. Please try again.";
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
                    await _auditService.LogAsync("med_category", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent medical category record with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("med_category", id.ToString(),
                    $"Edit form accessed for medical category: {item.MedCatName}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_category", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedCategory model)
        {
            var recordId = model.MedCatId.ToString();
            MedCategory? oldCategory = null;

            try
            {
                // Get the current category for audit comparison
                oldCategory = await _repo.GetByIdAsync(model.MedCatId);
                if (oldCategory == null)
                {
                    await _auditService.LogAsync("med_category", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent medical category record");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("med_category", "UPDATE_ATTEMPT", recordId, oldCategory, model,
                    $"Medical category record update attempt for: {oldCategory.MedCatName}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_category", "UPDATE_SECURITY_VIOLATION", recordId, oldCategory, model,
                        "Insecure input detected during medical category record update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate category name (excluding current record)
                if (await _repo.IsCategoryNameExistsAsync(model.MedCatName, model.MedCatId))
                {
                    ModelState.AddModelError("MedCatName", "A medical category with this name already exists. Please choose a different name.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_category", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldCategory, model,
                        $"Attempted to update to duplicate medical category: {model.MedCatName}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_category", "UPDATE_VALIDATION_FAILED", recordId, oldCategory, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medcategory_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_category", "UPDATE_RATE_LIMITED", recordId, oldCategory, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 MedCategory records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("med_category", recordId, oldCategory, model,
                    $"Medical category '{model.MedCatName}' updated successfully");

                return Json(new { success = true, message = "Medical category updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_category", "UPDATE_FAILED", recordId, oldCategory, model,
                    $"Medical category record update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedCategory_MedCatName_Unique") == true)
                {
                    ModelState.AddModelError("MedCatName", "A medical category with this name already exists. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the medical category. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedCategory? categoryToDelete = null;

            try
            {
                // Get entity before deletion for audit
                categoryToDelete = await _repo.GetByIdAsync(id);
                if (categoryToDelete == null)
                {
                    await _auditService.LogAsync("med_category", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent medical category record with ID: {id}");

                    return Json(new { success = false, message = "Medical category record not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("med_category", "DELETE_ATTEMPT", id.ToString(), categoryToDelete, null,
                    $"Medical category record deletion attempt for: {categoryToDelete.MedCatName}");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("med_category", id.ToString(), categoryToDelete,
                    $"Medical category '{categoryToDelete.MedCatName}' deleted successfully");

                return Json(new { success = true, message = "Medical category deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_category", "DELETE_FAILED", id.ToString(), categoryToDelete, null,
                    $"Medical category record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the medical category." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("med_category", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent medical category record with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("med_category", id.ToString(),
                    $"Medical category record details viewed: {item.MedCatName}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_category", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading medical category record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckCategoryNameExists(string categoryName, int? categoryId = null)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return Json(new { exists = false });

            // Sanitize input before checking
            categoryName = SanitizeString(categoryName);

            var exists = await _repo.IsCategoryNameExistsAsync(categoryName, categoryId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedCategory SanitizeInput(MedCategory model)
        {
            if (model == null) return model;

            model.MedCatName = SanitizeString(model.MedCatName);
            model.Description = SanitizeString(model.Description);
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

        private bool IsInputSecure(MedCategory model)
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

            var inputsToCheck = new[] { model.MedCatName, model.Description, model.Remarks };

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