using Microsoft.AspNetCore.Mvc;
using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;
using EMS.WebApp.Extensions;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedRefHospital")]
    public class MedRefHospitalController : Controller
    {
        private readonly IMedRefHospitalRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedRefHospitalController(IMedRefHospitalRepository repo, IMemoryCache cache, IAuditService auditService)
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
                await _auditService.LogAsync("med_ref_hospital", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} hospital records for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("med_ref_hospital", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load hospital records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("med_ref_hospital", "CREATE_FORM_VIEW", "new", null, null,
                "Create hospital record form accessed"));

            return PartialView("_CreateEdit", new MedRefHospital());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedRefHospital model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("med_ref_hospital", "CREATE_ATTEMPT", recordId, null, model,
                    "Hospital record creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_ref_hospital", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during hospital record creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate hospital name and code combination
                if (await _repo.IsHospitalNameCodeExistsAsync(model.hosp_name, model.hosp_code))
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_ref_hospital", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate hospital: {model.hosp_name} (Code: {model.hosp_code})");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_ref_hospital", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medrefhospital_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_ref_hospital", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 MedRefHospital records every 5 minutes. Please wait and try again.";
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
                recordId = model.hosp_id.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("med_ref_hospital", recordId, model,
                    $"Hospital record '{model.hosp_name}' (Code: {model.hosp_code}) created successfully");

                return Json(new { success = true, message = "Hospital record created successfully!", hospId = model.hosp_id });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("med_ref_hospital", "CREATE_FAILED", recordId, null, model,
                    $"Hospital record creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedRefHospital_HospNameCode_Unique") == true)
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the hospital record. Please try again.";
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
                    await _auditService.LogAsync("med_ref_hospital", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent hospital record with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("med_ref_hospital", id.ToString(),
                    $"Edit form accessed for hospital: {item.hosp_name} (Code: {item.hosp_code})");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_ref_hospital", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedRefHospital model)
        {
            var recordId = model.hosp_id.ToString();
            MedRefHospital? oldHospital = null;

            try
            {
                // Get the current hospital for audit comparison
                oldHospital = await _repo.GetByIdAsync(model.hosp_id);
                if (oldHospital == null)
                {
                    await _auditService.LogAsync("med_ref_hospital", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent hospital record");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("med_ref_hospital", "UPDATE_ATTEMPT", recordId, oldHospital, model,
                    $"Hospital record update attempt for: {oldHospital.hosp_name} (Code: {oldHospital.hosp_code})");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_ref_hospital", "UPDATE_SECURITY_VIOLATION", recordId, oldHospital, model,
                        "Insecure input detected during hospital record update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate hospital name and code combination (excluding current record)
                if (await _repo.IsHospitalNameCodeExistsAsync(model.hosp_name, model.hosp_code, model.hosp_id))
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_ref_hospital", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldHospital, model,
                        $"Attempted to update to duplicate hospital: {model.hosp_name} (Code: {model.hosp_code})");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_ref_hospital", "UPDATE_VALIDATION_FAILED", recordId, oldHospital, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medrefhospital_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_ref_hospital", "UPDATE_RATE_LIMITED", recordId, oldHospital, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 MedRefHospital records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("med_ref_hospital", recordId, oldHospital, model,
                    $"Hospital record '{model.hosp_name}' (Code: {model.hosp_code}) updated successfully");

                return Json(new { success = true, message = "Hospital record updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_ref_hospital", "UPDATE_FAILED", recordId, oldHospital, model,
                    $"Hospital record update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedRefHospital_HospNameCode_Unique") == true)
                {
                    ModelState.AddModelError("", "A hospital with this name and code combination already exists. Please choose a different name or code.");
                    ModelState.AddModelError("hosp_name", "This combination already exists.");
                    ModelState.AddModelError("hosp_code", "This combination already exists.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the hospital record. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedRefHospital? hospitalToDelete = null;

            try
            {
                // Get entity before deletion for audit
                hospitalToDelete = await _repo.GetByIdAsync(id);
                if (hospitalToDelete == null)
                {
                    await _auditService.LogAsync("med_ref_hospital", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent hospital record with ID: {id}");

                    return Json(new { success = false, message = "Hospital record not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("med_ref_hospital", "DELETE_ATTEMPT", id.ToString(), hospitalToDelete, null,
                    $"Hospital record deletion attempt for: {hospitalToDelete.hosp_name} (Code: {hospitalToDelete.hosp_code})");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("med_ref_hospital", id.ToString(), hospitalToDelete,
                    $"Hospital record '{hospitalToDelete.hosp_name}' (Code: {hospitalToDelete.hosp_code}) deleted successfully");

                return Json(new { success = true, message = "Hospital record deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_ref_hospital", "DELETE_FAILED", id.ToString(), hospitalToDelete, null,
                    $"Hospital record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the hospital record." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("med_ref_hospital", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent hospital record with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("med_ref_hospital", id.ToString(),
                    $"Hospital record details viewed: {item.hosp_name} (Code: {item.hosp_code})");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_ref_hospital", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading hospital record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckHospitalNameCodeExists(string hospName, string hospCode, int? hospId = null)
        {
            if (string.IsNullOrWhiteSpace(hospName) || string.IsNullOrWhiteSpace(hospCode))
                return Json(new { exists = false });

            // Sanitize input before checking
            hospName = SanitizeString(hospName);
            hospCode = SanitizeString(hospCode);

            var exists = await _repo.IsHospitalNameCodeExistsAsync(hospName, hospCode, hospId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedRefHospital SanitizeInput(MedRefHospital model)
        {
            if (model == null) return model;

            model.hosp_name = SanitizeString(model.hosp_name);
            model.hosp_code = SanitizeString(model.hosp_code);
            model.speciality = SanitizeString(model.speciality);
            model.address = SanitizeString(model.address);
            model.description = SanitizeString(model.description);
            model.vendor_name = SanitizeString(model.vendor_name);
            model.vendor_code = SanitizeString(model.vendor_code);
            model.contact_person_name = SanitizeString(model.contact_person_name);
            model.contact_person_email_id = SanitizeString(model.contact_person_email_id);

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

        private bool IsInputSecure(MedRefHospital model)
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

            var inputsToCheck = new[] {
                model.hosp_name,
                model.hosp_code,
                model.speciality,
                model.address,
                model.description,
                model.vendor_name,
                model.vendor_code,
                model.contact_person_name,
                model.contact_person_email_id
            };

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