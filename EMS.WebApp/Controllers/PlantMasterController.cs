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
    [Authorize("AccessPlantMaster")]
    public class PlantMasterController : Controller
    {
        private readonly IPlantMasterRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public PlantMasterController(IPlantMasterRepository repo, IMemoryCache cache, IAuditService auditService)
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
                await _auditService.LogAsync("org_plant", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} plants for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("org_plant", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load plants: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("org_plant", "CREATE_FORM_VIEW", "new", null, null,
                "Create plant form accessed"));

            return PartialView("_CreateEdit", new OrgPlant());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrgPlant model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("org_plant", "CREATE_ATTEMPT", recordId, null, model,
                    "Plant creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("org_plant", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during plant creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate plant code
                if (await _repo.IsPlantCodeExistsAsync(model.plant_code))
                {
                    ModelState.AddModelError("plant_code", "A plant with this code already exists. Please choose a different code.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("org_plant", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate plant code: {model.plant_code}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("org_plant", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_plant_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("org_plant", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 plants every 5 minutes. Please wait and try again.";
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
                recordId = model.plant_id.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("org_plant", recordId, model,
                    $"Plant '{model.plant_name}' (Code: {model.plant_code}) created successfully");

                return Json(new { success = true, message = "Plant created successfully!", plantId = model.plant_id });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("org_plant", "CREATE_FAILED", recordId, null, model,
                    $"Plant creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_OrgPlant_PlantCode_Unique") == true)
                {
                    ModelState.AddModelError("plant_code", "A plant with this code already exists. Please choose a different code.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the plant. Please try again.";
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
                    await _auditService.LogAsync("org_plant", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent plant with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("org_plant", id.ToString(),
                    $"Edit form accessed for plant: {item.plant_name} (Code: {item.plant_code})");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("org_plant", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OrgPlant model)
        {
            var recordId = model.plant_id.ToString();
            OrgPlant? oldPlant = null;

            try
            {
                // Get the current plant for audit comparison
                oldPlant = await _repo.GetByIdAsync(model.plant_id);
                if (oldPlant == null)
                {
                    await _auditService.LogAsync("org_plant", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent plant");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("org_plant", "UPDATE_ATTEMPT", recordId, oldPlant, model,
                    $"Plant update attempt for: {oldPlant.plant_name} (Code: {oldPlant.plant_code})");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("org_plant", "UPDATE_SECURITY_VIOLATION", recordId, oldPlant, model,
                        "Insecure input detected during plant update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate plant code (excluding current record)
                if (await _repo.IsPlantCodeExistsAsync(model.plant_code, model.plant_id))
                {
                    ModelState.AddModelError("plant_code", "A plant with this code already exists. Please choose a different code.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("org_plant", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldPlant, model,
                        $"Attempted to update to duplicate plant code: {model.plant_code}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("org_plant", "UPDATE_VALIDATION_FAILED", recordId, oldPlant, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_plant_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("org_plant", "UPDATE_RATE_LIMITED", recordId, oldPlant, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 plants every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("org_plant", recordId, oldPlant, model,
                    $"Plant '{model.plant_name}' (Code: {model.plant_code}) updated successfully");

                return Json(new { success = true, message = "Plant updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("org_plant", "UPDATE_FAILED", recordId, oldPlant, model,
                    $"Plant update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_OrgPlant_PlantCode_Unique") == true)
                {
                    ModelState.AddModelError("plant_code", "A plant with this code already exists. Please choose a different code.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the plant. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(short id)
        {
            OrgPlant? plantToDelete = null;

            try
            {
                // Get entity before deletion for audit
                plantToDelete = await _repo.GetByIdAsync(id);
                if (plantToDelete == null)
                {
                    await _auditService.LogAsync("org_plant", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent plant with ID: {id}");

                    return Json(new { success = false, message = "Plant not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("org_plant", "DELETE_ATTEMPT", id.ToString(), plantToDelete, null,
                    $"Plant deletion attempt for: {plantToDelete.plant_name} (Code: {plantToDelete.plant_code})");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("org_plant", id.ToString(), plantToDelete,
                    $"Plant '{plantToDelete.plant_name}' (Code: {plantToDelete.plant_code}) deleted successfully");

                return Json(new { success = true, message = "Plant deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("org_plant", "DELETE_FAILED", id.ToString(), plantToDelete, null,
                    $"Plant deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the plant." });
            }
        }

        public async Task<IActionResult> Details(short id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("org_plant", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent plant with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("org_plant", id.ToString(),
                    $"Plant details viewed: {item.plant_name} (Code: {item.plant_code})");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("org_plant", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading plant details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckPlantCodeExists(string plantCode, short? plantId = null)
        {
            if (string.IsNullOrWhiteSpace(plantCode))
                return Json(new { exists = false });

            // Sanitize input before checking
            plantCode = SanitizeString(plantCode);

            var exists = await _repo.IsPlantCodeExistsAsync(plantCode, plantId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private OrgPlant SanitizeInput(OrgPlant model)
        {
            if (model == null) return model;

            model.plant_code = SanitizeString(model.plant_code);
            model.plant_name = SanitizeString(model.plant_name);
            model.Description = SanitizeString(model.Description);

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

        private bool IsInputSecure(OrgPlant model)
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

            var inputsToCheck = new[] { model.plant_code, model.plant_name, model.Description };

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