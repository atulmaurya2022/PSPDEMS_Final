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
    [Authorize("AccessMedDiagnosis")]
    public class MedDiagnosisController : Controller
    {
        private readonly IMedDiagnosisRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedDiagnosisController(IMedDiagnosisRepository repo, IMemoryCache cache, IAuditService auditService)
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

                await _auditService.LogAsync("med_diagnosis", "INDEX_VIEW", "main", null, null,
                    $"Med diagnosis module accessed by user, Plant: {userPlantId}");
                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "INDEX_FAILED", "main", null, null,
                    $"Failed to load med diagnosis index: {ex.Message}");
                throw;
            }
        }

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                await _auditService.LogAsync("med_diagnosis", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - User: {currentUser}, Plant: {userPlantId}");

                var list = await _repo.ListAsync(userPlantId);

                // Transform data to include plant information
                var result = list.Select(x => new
                {
                    x.diag_id,
                    x.diag_name,
                    x.diag_desc,
                    x.CreatedBy,
                    CreatedOn = x.CreatedOn?.ToString("dd/MM/yyyy HH:mm"),
                    x.ModifiedBy,
                    ModifiedOn = x.ModifiedOn?.ToString("dd/MM/yyyy HH:mm"),
                    PlantName = x.OrgPlant?.plant_name ?? "Unknown Plant"
                }).ToList();

                await _auditService.LogAsync("med_diagnosis", "LOAD_DATA_SUCCESS", "multiple", null, null,
                    $"Loaded {result.Count} diagnosis records for listing, Plant: {userPlantId}");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load diagnosis records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_diagnosis", "CREATE_FORM_VIEW", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_diagnosis", "CREATE_NO_PLANT", "new", null, null,
                        "Create failed - user has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                var model = new MedDiagnosis
                {
                    plant_id = (short)userPlantId.Value
                };

                await _auditService.LogAsync("med_diagnosis", "CREATE_FORM_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "CREATE_FORM_ERROR", "new", null, null,
                    $"Create form error: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedDiagnosis model)
        {
            string recordId = "new";

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.diag_id.ToString();

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_diagnosis", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    return PartialView("_CreateEdit", model);
                }

                model.plant_id = (short)userPlantId.Value;

                await _auditService.LogAsync("med_diagnosis", "CREATE_ATTEMPT", recordId, null, model,
                    $"Diagnosis record creation attempt started for plant: {model.plant_id}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("med_diagnosis", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during diagnosis record creation");

                    return PartialView("_CreateEdit", model);
                }

                // Remove plant_id from ModelState validation as it's set programmatically
                ModelState.Remove("plant_id");

                // Check for duplicate diagnosis name within the same plant
                if (await _repo.IsDiagnosisNameExistsAsync(model.diag_name, null, userPlantId))
                {
                    ModelState.AddModelError("diag_name", "A diagnosis with this name already exists in your plant. Please choose a different name.");

                    await _auditService.LogAsync("med_diagnosis", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate diagnosis in plant {userPlantId}: {model.diag_name}");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_diagnosis", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_meddiagnosis_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("med_diagnosis", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 MedDiagnosis records every 5 minutes. Please wait and try again.";
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
                recordId = model.diag_id.ToString();

                await _auditService.LogCreateAsync("med_diagnosis", recordId, model,
                    $"Diagnosis record '{model.diag_name}' created successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Diagnosis record created successfully!", diagId = model.diag_id });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "CREATE_FAILED", recordId, null, model,
                    $"Diagnosis record creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
                {
                    ViewBag.Error = "Plant assignment error. Please contact administrator.";
                }
                else if (ex.InnerException?.Message.Contains("plant_id") == true)
                {
                    ViewBag.Error = "Invalid plant assignment. Please refresh and try again.";
                }
                else if (ex.InnerException?.Message.Contains("IX_MedDiagnosis_DiagName_Unique") == true)
                {
                    ModelState.AddModelError("diag_name", "A diagnosis with this name already exists in your plant. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the diagnosis record. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_diagnosis", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed for plant: {userPlantId}");

                var item = await _repo.GetByIdAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("med_diagnosis", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent diagnosis record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                await _auditService.LogViewAsync("med_diagnosis", id.ToString(),
                    $"Edit form accessed for diagnosis: {item.diag_name} in plant: {item.OrgPlant?.plant_name}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedDiagnosis model)
        {
            var recordId = model.diag_id.ToString();
            MedDiagnosis? oldDiagnosis = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get the current diagnosis for audit comparison and plant validation
                oldDiagnosis = await _repo.GetByIdAsync(model.diag_id, userPlantId);
                if (oldDiagnosis == null)
                {
                    await _auditService.LogAsync("med_diagnosis", "EDIT_NOT_FOUND", recordId, null, model,
                        $"Attempted to edit non-existent diagnosis record or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && oldDiagnosis.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("med_diagnosis", "EDIT_PLANT_DENY", recordId, oldDiagnosis, model,
                        $"Edit denied - record belongs to different plant: {oldDiagnosis.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only edit records from your assigned plant." });
                }

                await _auditService.LogAsync("med_diagnosis", "UPDATE_ATTEMPT", recordId, oldDiagnosis, model,
                    $"Diagnosis record update attempt for: {oldDiagnosis.diag_name} in plant: {oldDiagnosis.plant_id}");

                // Preserve plant_id from original record (don't allow changing plant)
                model.plant_id = oldDiagnosis.plant_id;

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("med_diagnosis", "UPDATE_SECURITY_VIOLATION", recordId, oldDiagnosis, model,
                        "Insecure input detected during diagnosis record update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate diagnosis name within the same plant (excluding current record)
                if (await _repo.IsDiagnosisNameExistsAsync(model.diag_name, model.diag_id, userPlantId))
                {
                    ModelState.AddModelError("diag_name", "A diagnosis with this name already exists in your plant. Please choose a different name.");

                    await _auditService.LogAsync("med_diagnosis", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldDiagnosis, model,
                        $"Attempted to update to duplicate diagnosis in plant {userPlantId}: {model.diag_name}");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_diagnosis", "UPDATE_VALIDATION_FAILED", recordId, oldDiagnosis, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_meddiagnosis_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("med_diagnosis", "UPDATE_RATE_LIMITED", recordId, oldDiagnosis, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 MedDiagnosis records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                await _auditService.LogUpdateAsync("med_diagnosis", recordId, oldDiagnosis, model,
                    $"Diagnosis record '{model.diag_name}' updated successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Diagnosis record updated successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "UPDATE_FAILED", recordId, oldDiagnosis, model,
                    $"Diagnosis record update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("IX_MedDiagnosis_DiagName_Unique") == true)
                {
                    ModelState.AddModelError("diag_name", "A diagnosis with this name already exists in your plant. Please choose a different name.");
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the diagnosis record. Please try again.";
                }

                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedDiagnosis? diagnosisToDelete = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get entity before deletion for audit and plant validation
                diagnosisToDelete = await _repo.GetByIdAsync(id, userPlantId);
                if (diagnosisToDelete == null)
                {
                    await _auditService.LogAsync("med_diagnosis", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent diagnosis record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return Json(new { success = false, message = "Diagnosis record not found or access denied." });
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && diagnosisToDelete.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("med_diagnosis", "DELETE_PLANT_DENY", id.ToString(), diagnosisToDelete, null,
                        $"Delete denied - record belongs to different plant: {diagnosisToDelete.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only delete records from your assigned plant." });
                }

                await _auditService.LogAsync("med_diagnosis", "DELETE_ATTEMPT", id.ToString(), diagnosisToDelete, null,
                    $"Diagnosis record deletion attempt for: {diagnosisToDelete.diag_name} in plant: {diagnosisToDelete.plant_id}");

                await _repo.DeleteAsync(id, userPlantId);

                await _auditService.LogDeleteAsync("med_diagnosis", id.ToString(), diagnosisToDelete,
                    $"Diagnosis record '{diagnosisToDelete.diag_name}' deleted successfully from plant: {diagnosisToDelete.plant_id}");

                return Json(new { success = true, message = "Diagnosis record deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "DELETE_FAILED", id.ToString(), diagnosisToDelete, null,
                    $"Diagnosis record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the diagnosis record." });
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
                    await _auditService.LogAsync("med_diagnosis", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent diagnosis record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                await _auditService.LogViewAsync("med_diagnosis", id.ToString(),
                    $"Diagnosis record details viewed: {item.diag_name} in plant: {item.OrgPlant?.plant_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_diagnosis", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading diagnosis record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation with plant filtering
        [HttpPost]
        public async Task<IActionResult> CheckDiagnosisNameExists(string diagnosisName, int? diagnosisId = null)
        {
            if (string.IsNullOrWhiteSpace(diagnosisName))
                return Json(new { exists = false });

            // Sanitize input before checking
            diagnosisName = SanitizeString(diagnosisName);

            var userPlantId = await GetCurrentUserPlantIdAsync();
            var exists = await _repo.IsDiagnosisNameExistsAsync(diagnosisName, diagnosisId, userPlantId);
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
                await _auditService.LogAsync("med_diagnosis", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        #region Private Methods for Input Sanitization and Validation

        private MedDiagnosis SanitizeInput(MedDiagnosis model)
        {
            if (model == null) return model;

            model.diag_name = SanitizeString(model.diag_name);
            model.diag_desc = SanitizeString(model.diag_desc);

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

        private bool IsInputSecure(MedDiagnosis model)
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

            var inputsToCheck = new[] { model.diag_name, model.diag_desc };

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

        #endregion
    }
}