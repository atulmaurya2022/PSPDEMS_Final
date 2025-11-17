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
    [Authorize("AccessMedAmbulanceMaster")]
    public class MedAmbulanceMasterController : Controller
    {
        private readonly IMedAmbulanceMasterRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedAmbulanceMasterController(IMedAmbulanceMasterRepository repo, IMemoryCache cache, IAuditService auditService)
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
                await _auditService.LogAsync("med_ambulance_master", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} ambulance records for listing");

                return Json(new { data = list });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("med_ambulance_master", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load ambulance records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public IActionResult Create()
        {
            // Log form access
            _ = Task.Run(async () => await _auditService.LogAsync("med_ambulance_master", "CREATE_FORM_VIEW", "new", null, null,
                "Create ambulance record form accessed"));

            return PartialView("_CreateEdit", new MedAmbulanceMaster());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedAmbulanceMaster model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("med_ambulance_master", "CREATE_ATTEMPT", recordId, null, model,
                    "Ambulance record creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_ambulance_master", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during ambulance record creation");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate vehicle number
                if (await _repo.IsVehicleNumberExistsAsync(model.vehicle_no))
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_ambulance_master", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate vehicle number: {model.vehicle_no}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_ambulance_master", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_ambulance_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_ambulance_master", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 ambulance records every 5 minutes. Please wait and try again.";
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
                recordId = model.amb_id.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("med_ambulance_master", recordId, model,
                    $"Ambulance record '{model.vehicle_no}' (Provider: {model.provider}, Type: {model.vehicle_type}) created successfully");

                return Json(new { success = true, message = "Ambulance record created successfully!", ambId = model.amb_id });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("med_ambulance_master", "CREATE_FAILED", recordId, null, model,
                    $"Ambulance record creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedAmbulanceMaster_VehicleNo_Unique") == true)
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the ambulance record. Please try again.";
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
                    await _auditService.LogAsync("med_ambulance_master", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent ambulance record with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                await _auditService.LogViewAsync("med_ambulance_master", id.ToString(),
                    $"Edit form accessed for ambulance: {item.vehicle_no} (Provider: {item.provider}, Type: {item.vehicle_type})");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_ambulance_master", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedAmbulanceMaster model)
        {
            var recordId = model.amb_id.ToString();
            MedAmbulanceMaster? oldAmbulance = null;

            try
            {
                // Get the current ambulance for audit comparison
                oldAmbulance = await _repo.GetByIdAsync(model.amb_id);
                if (oldAmbulance == null)
                {
                    await _auditService.LogAsync("med_ambulance_master", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent ambulance record");

                    return NotFound();
                }

                // Log the update attempt
                await _auditService.LogAsync("med_ambulance_master", "UPDATE_ATTEMPT", recordId, oldAmbulance, model,
                    $"Ambulance record update attempt for: {oldAmbulance.vehicle_no} (Provider: {oldAmbulance.provider}, Type: {oldAmbulance.vehicle_type})");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_ambulance_master", "UPDATE_SECURITY_VIOLATION", recordId, oldAmbulance, model,
                        "Insecure input detected during ambulance record update");

                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate vehicle number (excluding current record)
                if (await _repo.IsVehicleNumberExistsAsync(model.vehicle_no, model.amb_id))
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_ambulance_master", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldAmbulance, model,
                        $"Attempted to update to duplicate vehicle number: {model.vehicle_no}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_ambulance_master", "UPDATE_VALIDATION_FAILED", recordId, oldAmbulance, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_ambulance_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_ambulance_master", "UPDATE_RATE_LIMITED", recordId, oldAmbulance, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 ambulance records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("med_ambulance_master", recordId, oldAmbulance, model,
                    $"Ambulance record '{model.vehicle_no}' (Provider: {model.provider}, Type: {model.vehicle_type}) updated successfully");

                return Json(new { success = true, message = "Ambulance record updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_ambulance_master", "UPDATE_FAILED", recordId, oldAmbulance, model,
                    $"Ambulance record update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedAmbulanceMaster_VehicleNo_Unique") == true)
                {
                    ModelState.AddModelError("", "A vehicle with this number already exists. Please choose a different vehicle number.");
                    ModelState.AddModelError("vehicle_no", "This vehicle number already exists.");
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the ambulance record. Please try again.";
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedAmbulanceMaster? ambulanceToDelete = null;

            try
            {
                // Get entity before deletion for audit
                ambulanceToDelete = await _repo.GetByIdAsync(id);
                if (ambulanceToDelete == null)
                {
                    await _auditService.LogAsync("med_ambulance_master", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent ambulance record with ID: {id}");

                    return Json(new { success = false, message = "Ambulance record not found." });
                }

                // Log deletion attempt
                await _auditService.LogAsync("med_ambulance_master", "DELETE_ATTEMPT", id.ToString(), ambulanceToDelete, null,
                    $"Ambulance record deletion attempt for: {ambulanceToDelete.vehicle_no} (Provider: {ambulanceToDelete.provider}, Type: {ambulanceToDelete.vehicle_type})");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("med_ambulance_master", id.ToString(), ambulanceToDelete,
                    $"Ambulance record '{ambulanceToDelete.vehicle_no}' (Provider: {ambulanceToDelete.provider}, Type: {ambulanceToDelete.vehicle_type}) deleted successfully");

                return Json(new { success = true, message = "Ambulance record deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_ambulance_master", "DELETE_FAILED", id.ToString(), ambulanceToDelete, null,
                    $"Ambulance record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the ambulance record." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("med_ambulance_master", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent ambulance record with ID: {id}");

                    return NotFound();
                }

                // Log details view
                await _auditService.LogViewAsync("med_ambulance_master", id.ToString(),
                    $"Ambulance record details viewed: {item.vehicle_no} (Provider: {item.provider}, Type: {item.vehicle_type})");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_ambulance_master", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading ambulance record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckVehicleNumberExists(string vehicleNo, int? ambId = null)
        {
            if (string.IsNullOrWhiteSpace(vehicleNo))
                return Json(new { exists = false });

            // Sanitize input before checking
            vehicleNo = SanitizeString(vehicleNo);

            var exists = await _repo.IsVehicleNumberExistsAsync(vehicleNo, ambId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedAmbulanceMaster SanitizeInput(MedAmbulanceMaster model)
        {
            if (model == null) return model;

            model.vehicle_no = SanitizeString(model.vehicle_no);
            model.provider = SanitizeString(model.provider);
            model.vehicle_type = SanitizeString(model.vehicle_type);

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

        private bool IsInputSecure(MedAmbulanceMaster model)
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

            var inputsToCheck = new[] { model.vehicle_no, model.provider, model.vehicle_type };

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