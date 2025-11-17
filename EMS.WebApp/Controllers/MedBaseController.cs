using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedBase")]
    public class MedBaseController : Controller
    {
        private readonly IMedBaseRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedBaseController(IMedBaseRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_base", "INDEX_VIEW", "main", null, null,
                    $"Med base module accessed by user, Plant: {userPlantId}");
                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "INDEX_FAILED", "main", null, null,
                    $"Failed to load med base index: {ex.Message}");
                throw;
            }
        }

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                await _auditService.LogAsync("med_base", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - User: {currentUser}, Plant: {userPlantId}");

                var list = await _repo.ListAsync(userPlantId);

                // Transform data to include plant information
                var result = list.Select(x => new
                {
                    x.BaseId,
                    x.BaseName,
                    x.BaseDesc,
                    x.CreatedBy,
                    CreatedOn = x.CreatedOn?.ToString("dd/MM/yyyy HH:mm"),
                    x.ModifiedBy,
                    ModifiedOn = x.ModifiedOn?.ToString("dd/MM/yyyy HH:mm"),
                    PlantName = x.OrgPlant?.plant_name ?? "Unknown Plant"
                }).ToList();

                await _auditService.LogAsync("med_base", "LOAD_DATA_SUCCESS", "multiple", null, null,
                    $"Loaded {result.Count} medical base records for listing, Plant: {userPlantId}");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load medical base records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_base", "CREATE_FORM_VIEW", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_base", "CREATE_NO_PLANT", "new", null, null,
                        "Create failed - user has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                var model = new MedBase
                {
                    plant_id = (short)userPlantId.Value
                };

                await _auditService.LogAsync("med_base", "CREATE_FORM_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "CREATE_FORM_ERROR", "new", null, null,
                    $"Create form error: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedBase model)
        {
            string recordId = "new";

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.BaseId.ToString();

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_base", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    return PartialView("_CreateEdit", model);
                }

                model.plant_id = (short)userPlantId.Value;

                await _auditService.LogAsync("med_base", "CREATE_ATTEMPT", recordId, null, model,
                    $"Medical base record creation attempt started for plant: {model.plant_id}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("med_base", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during medical base record creation");

                    return PartialView("_CreateEdit", model);
                }

                // Remove plant_id from ModelState validation as it's set programmatically
                ModelState.Remove("plant_id");

                // Check for duplicate base name within the same plant
                if (await _repo.IsBaseNameExistsAsync(model.BaseName, null, userPlantId))
                {
                    ModelState.AddModelError("BaseName", "A medical base with this name already exists in your plant. Please choose a different name.");

                    await _auditService.LogAsync("med_base", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate medical base: {model.BaseName} in plant: {userPlantId}");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_base", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medbase_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("med_base", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 MedBase records every 5 minutes. Please wait and try again.";
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
                recordId = model.BaseId.ToString();

                await _auditService.LogCreateAsync("med_base", recordId, model,
                    $"Medical base '{model.BaseName}' created successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Medical base created successfully!", baseId = model.BaseId });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "CREATE_FAILED", recordId, null, model,
                    $"Medical base record creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
                {
                    ViewBag.Error = "Plant assignment error. Please contact administrator.";
                }
                else if (ex.InnerException?.Message.Contains("plant_id") == true)
                {
                    ViewBag.Error = "Invalid plant assignment. Please refresh and try again.";
                }
                else if (ex.InnerException?.Message.Contains("IX_MedBase_BaseName_Unique") == true)
                {
                    ModelState.AddModelError("BaseName", "A medical base with this name already exists in your plant. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the medical base. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_base", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed for plant: {userPlantId}");

                var item = await _repo.GetByIdAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("med_base", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent medical base record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                await _auditService.LogViewAsync("med_base", id.ToString(),
                    $"Edit form accessed for medical base: {item.BaseName} in plant: {item.OrgPlant?.plant_name}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedBase model)
        {
            var recordId = model.BaseId.ToString();
            MedBase? oldBase = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get the current base for audit comparison and plant validation
                oldBase = await _repo.GetByIdAsync(model.BaseId, userPlantId);
                if (oldBase == null)
                {
                    await _auditService.LogAsync("med_base", "EDIT_NOT_FOUND", recordId, null, model,
                        $"Attempted to edit non-existent medical base record or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && oldBase.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("med_base", "EDIT_PLANT_DENY", recordId, oldBase, model,
                        $"Edit denied - record belongs to different plant: {oldBase.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only edit records from your assigned plant." });
                }

                await _auditService.LogAsync("med_base", "UPDATE_ATTEMPT", recordId, oldBase, model,
                    $"Medical base record update attempt for: {oldBase.BaseName} in plant: {oldBase.plant_id}");

                // Preserve plant_id from original record (don't allow changing plant)
                model.plant_id = oldBase.plant_id;

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("med_base", "UPDATE_SECURITY_VIOLATION", recordId, oldBase, model,
                        "Insecure input detected during medical base record update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate base name within the same plant (excluding current record)
                if (await _repo.IsBaseNameExistsAsync(model.BaseName, model.BaseId, userPlantId))
                {
                    ModelState.AddModelError("BaseName", "A medical base with this name already exists in your plant. Please choose a different name.");

                    await _auditService.LogAsync("med_base", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldBase, model,
                        $"Attempted to update to duplicate medical base: {model.BaseName} in plant: {userPlantId}");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_base", "UPDATE_VALIDATION_FAILED", recordId, oldBase, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medbase_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("med_base", "UPDATE_RATE_LIMITED", recordId, oldBase, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 MedBase records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                await _auditService.LogUpdateAsync("med_base", recordId, oldBase, model,
                    $"Medical base '{model.BaseName}' updated successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Medical base updated successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "UPDATE_FAILED", recordId, oldBase, model,
                    $"Medical base record update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("IX_MedBase_BaseName_Unique") == true)
                {
                    ModelState.AddModelError("BaseName", "A medical base with this name already exists in your plant. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the medical base. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedBase? baseToDelete = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get entity before deletion for audit and plant validation
                baseToDelete = await _repo.GetByIdAsync(id, userPlantId);
                if (baseToDelete == null)
                {
                    await _auditService.LogAsync("med_base", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent medical base record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return Json(new { success = false, message = "Medical base record not found or access denied." });
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && baseToDelete.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("med_base", "DELETE_PLANT_DENY", id.ToString(), baseToDelete, null,
                        $"Delete denied - record belongs to different plant: {baseToDelete.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only delete records from your assigned plant." });
                }

                await _auditService.LogAsync("med_base", "DELETE_ATTEMPT", id.ToString(), baseToDelete, null,
                    $"Medical base record deletion attempt for: {baseToDelete.BaseName} in plant: {baseToDelete.plant_id}");

                await _repo.DeleteAsync(id, userPlantId);

                await _auditService.LogDeleteAsync("med_base", id.ToString(), baseToDelete,
                    $"Medical base '{baseToDelete.BaseName}' deleted successfully from plant: {baseToDelete.plant_id}");

                return Json(new { success = true, message = "Medical base deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "DELETE_FAILED", id.ToString(), baseToDelete, null,
                    $"Medical base record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the medical base." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                var item = await _repo.GetByIdAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("med_base", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent medical base record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                await _auditService.LogViewAsync("med_base", id.ToString(),
                    $"Medical base record details viewed: {item.BaseName} in plant: {item.OrgPlant?.plant_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading medical base record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation with plant filtering
        [HttpPost]
        public async Task<IActionResult> CheckBaseNameExists(string baseName, int? baseId = null)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                return Json(new { exists = false });

            // Sanitize input before checking
            baseName = SanitizeString(baseName);

            var userPlantId = await GetCurrentUserPlantIdAsync();
            var exists = await _repo.IsBaseNameExistsAsync(baseName, baseId, userPlantId);
            return Json(new { exists = exists });
        }

        // Helper method to get current user's plant ID
        private async Task<int?> GetCurrentUserPlantIdAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                return await _repo.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_base", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        #region Private Methods for Input Sanitization and Validation

        private MedBase SanitizeInput(MedBase model)
        {
            if (model == null) return model;

            model.BaseName = SanitizeString(model.BaseName);
            model.BaseDesc = SanitizeString(model.BaseDesc);

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

        private bool IsInputSecure(MedBase model)
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

            var inputsToCheck = new[] { model.BaseName, model.BaseDesc };

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