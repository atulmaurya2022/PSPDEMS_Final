using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedMaster")]
    public class MedMasterController : Controller
    {
        private readonly IMedMasterRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedMasterController(IMedMasterRepository repo, IMemoryCache cache, IAuditService auditService)
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

                await _auditService.LogAsync("med_master", "INDEX_VIEW", "main", null, null,
                    $"Med master module accessed by user, Plant: {userPlantId}");
                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "INDEX_FAILED", "main", null, null,
                    $"Failed to load med master index: {ex.Message}");
                throw;
            }
        }

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                await _auditService.LogAsync("med_master", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - User: {currentUser}, Plant: {userPlantId}");

                var list = await _repo.ListWithBaseAsync(userPlantId);
                var result = list.Select(x => new {
                    x.MedItemId,
                    x.MedItemName,
                    BaseName = x.MedBase != null ? x.MedBase.BaseName : "",
                    x.CompanyName,
                    x.ReorderLimit,
                    x.CreatedBy,
                    CreatedOn = x.CreatedOn?.ToString("dd/MM/yyyy HH:mm"),
                    x.ModifiedBy,
                    ModifiedOn = x.ModifiedOn?.ToString("dd/MM/yyyy HH:mm"),
                    PlantName = x.OrgPlant?.plant_name ?? "Unknown Plant"
                }).ToList();

                await _auditService.LogAsync("med_master", "LOAD_DATA_SUCCESS", "multiple", null, null,
                    $"Loaded {result.Count} medicine records for listing, Plant: {userPlantId}");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load medicine records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_master", "CREATE_FORM_VIEW", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_master", "CREATE_NO_PLANT", "new", null, null,
                        "Create failed - user has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                var baseList = await _repo.GetBaseListAsync(userPlantId);

                if (!baseList.Any())
                {
                    ViewBag.MedBaseList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "⚠ No medicine bases found in your plant! Please create medicine bases first.";
                }
                else
                {
                    // Pick "Nill" (any case) as default for Create
                    var defaultBaseId = baseList
                        .FirstOrDefault(b => string.Equals(
                            (b.BaseName ?? string.Empty).Trim(),
                            "nill",
                            StringComparison.OrdinalIgnoreCase))
                        ?.BaseId;

                    // Use selectedValue overload; if Nill not found, selectedValue stays null (no preselect)
                    ViewBag.MedBaseList = new SelectList(baseList, "BaseId", "BaseName", defaultBaseId);
                }

                var model = new MedMaster
                {
                    plant_id = (short)userPlantId.Value
                };

                await _auditService.LogAsync("med_master", "CREATE_FORM_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "CREATE_FORM_ERROR", "new", null, null,
                    $"Create form error: {ex.Message}");

                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new MedMaster());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedMaster model)
        {
            string recordId = "new";

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.MedItemId.ToString();

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_master", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    ViewBag.MedBaseList = new SelectList(Enumerable.Empty<SelectListItem>());
                    return PartialView("_CreateEdit", model);
                }

                model.plant_id = (short)userPlantId.Value;

                await _auditService.LogAsync("med_master", "CREATE_ATTEMPT", recordId, null, model,
                    $"Medicine record creation attempt started for plant: {model.plant_id}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("med_master", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during medicine record creation");

                    ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "BaseId", "BaseName", model.BaseId);
                    return PartialView("_CreateEdit", model);
                }

                // Remove plant_id from ModelState validation as it's set programmatically
                ModelState.Remove("plant_id");

                var baseList = await _repo.GetBaseListAsync(userPlantId);
                ViewBag.MedBaseList = new SelectList(baseList, "BaseId", "BaseName", model.BaseId);
                string baseName = "";

                // Validate that the selected base belongs to the user's plant
                if (model.BaseId.HasValue && !baseList.Any(b => b.BaseId == model.BaseId.Value))
                {
                    ModelState.AddModelError("BaseId", "Selected medicine base is not available in your plant.");
                    await _auditService.LogAsync("med_master", "CREATE_BASE_INVALID", recordId, null, model,
                        $"Invalid base selection - BaseId: {model.BaseId} not in plant: {userPlantId}");
                }

                // Check for duplicate med item details combination within the same plant
                if (await _repo.IsMedItemDetailsExistsAsync(model.MedItemName, model.BaseId, model.CompanyName, null, userPlantId))
                {
                    ModelState.AddModelError("", "A medical item with this combination of name, base, and company already exists in your plant. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists in your plant.");
                    ModelState.AddModelError("BaseId", "This combination already exists in your plant.");
                    ModelState.AddModelError("CompanyName", "This combination already exists in your plant.");

                    baseName = baseList.FirstOrDefault(b => b.BaseId == model.BaseId)?.BaseName ?? "Unknown";
                    await _auditService.LogAsync("med_master", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate medicine in plant {userPlantId}: {model.MedItemName} (Base: {baseName}, Company: {model.CompanyName})");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_master", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("med_master", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 medicine records every 5 minutes. Please wait and try again.";
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
                recordId = model.MedItemId.ToString();

                // Get base name for better audit message
                baseName = baseList.FirstOrDefault(b => b.BaseId == model.BaseId)?.BaseName ?? "Unknown";

                await _auditService.LogCreateAsync("med_master", recordId, model,
                    $"Medicine record '{model.MedItemName}' (Base: {baseName}, Company: {model.CompanyName}) created successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Medicine record created successfully!", medItemId = model.MedItemId });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "CREATE_FAILED", recordId, null, model,
                    $"Medicine record creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
                {
                    ViewBag.Error = "Plant assignment error or invalid base selection. Please contact administrator.";
                }
                else if (ex.InnerException?.Message.Contains("plant_id") == true)
                {
                    ViewBag.Error = "Invalid plant assignment. Please refresh and try again.";
                }
                else if (ex.InnerException?.Message.Contains("IX_MedMaster_MedItemNameBaseIdCompanyName_Unique") == true)
                {
                    ModelState.AddModelError("", "A medical item with this combination already exists in your plant. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists in your plant.");
                    ModelState.AddModelError("BaseId", "This combination already exists in your plant.");
                    ModelState.AddModelError("CompanyName", "This combination already exists in your plant.");
                    ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(await GetCurrentUserPlantIdAsync()), "BaseId", "BaseName", model.BaseId);
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the medicine. Please try again.";
                }

                ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(await GetCurrentUserPlantIdAsync()), "BaseId", "BaseName", model.BaseId);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_master", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed for plant: {userPlantId}");

                var item = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("med_master", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent medicine record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                var baseName = item.MedBase?.BaseName ?? "Unknown";
                await _auditService.LogViewAsync("med_master", id.ToString(),
                    $"Edit form accessed for medicine: {item.MedItemName} (Base: {baseName}, Company: {item.CompanyName}) in plant: {item.OrgPlant?.plant_name}");

                ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "BaseId", "BaseName", item.BaseId);
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedMaster model)
        {
            var recordId = model.MedItemId.ToString();
            MedMaster? oldMedicine = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get the current medicine for audit comparison and plant validation
                oldMedicine = await _repo.GetByIdWithBaseAsync(model.MedItemId, userPlantId);
                if (oldMedicine == null)
                {
                    await _auditService.LogAsync("med_master", "EDIT_NOT_FOUND", recordId, null, model,
                        $"Attempted to edit non-existent medicine record or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && oldMedicine.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("med_master", "EDIT_PLANT_DENY", recordId, oldMedicine, model,
                        $"Edit denied - record belongs to different plant: {oldMedicine.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only edit records from your assigned plant." });
                }

                var oldBaseName = oldMedicine.MedBase?.BaseName ?? "Unknown";
                await _auditService.LogAsync("med_master", "UPDATE_ATTEMPT", recordId, oldMedicine, model,
                    $"Medicine record update attempt for: {oldMedicine.MedItemName} (Base: {oldBaseName}, Company: {oldMedicine.CompanyName}) in plant: {oldMedicine.plant_id}");

                // Preserve plant_id from original record (don't allow changing plant)
                model.plant_id = oldMedicine.plant_id;

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("med_master", "UPDATE_SECURITY_VIOLATION", recordId, oldMedicine, model,
                        "Insecure input detected during medicine record update");

                    ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "BaseId", "BaseName", model.BaseId);
                    return PartialView("_CreateEdit", model);
                }

                var baseList = await _repo.GetBaseListAsync(userPlantId);
                ViewBag.MedBaseList = new SelectList(baseList, "BaseId", "BaseName", model.BaseId);

                // Validate that the selected base belongs to the user's plant
                if (model.BaseId.HasValue && !baseList.Any(b => b.BaseId == model.BaseId.Value))
                {
                    ModelState.AddModelError("BaseId", "Selected medicine base is not available in your plant.");
                    await _auditService.LogAsync("med_master", "UPDATE_BASE_INVALID", recordId, oldMedicine, model,
                        $"Invalid base selection - BaseId: {model.BaseId} not in plant: {userPlantId}");
                }

                // Check for duplicate med item details combination within the same plant (excluding current record)
                if (await _repo.IsMedItemDetailsExistsAsync(model.MedItemName, model.BaseId, model.CompanyName, model.MedItemId, userPlantId))
                {
                    ModelState.AddModelError("", "A medical item with this combination of name, base, and company already exists in your plant. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists in your plant.");
                    ModelState.AddModelError("BaseId", "This combination already exists in your plant.");
                    ModelState.AddModelError("CompanyName", "This combination already exists in your plant.");

                    var baseName = baseList.FirstOrDefault(b => b.BaseId == model.BaseId)?.BaseName ?? "Unknown";
                    await _auditService.LogAsync("med_master", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldMedicine, model,
                        $"Attempted to update to duplicate medicine in plant {userPlantId}: {model.MedItemName} (Base: {baseName}, Company: {model.CompanyName})");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_master", "UPDATE_VALIDATION_FAILED", recordId, oldMedicine, model,
                        $"Validation failed: {validationErrors}");

                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medmaster_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("med_master", "UPDATE_RATE_LIMITED", recordId, oldMedicine, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 medicine records every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Get updated base name for better audit message
                var updatedBaseName = baseList.FirstOrDefault(b => b.BaseId == model.BaseId)?.BaseName ?? "Unknown";

                await _auditService.LogUpdateAsync("med_master", recordId, oldMedicine, model,
                    $"Medicine record '{model.MedItemName}' (Base: {updatedBaseName}, Company: {model.CompanyName}) updated successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Medicine record updated successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "UPDATE_FAILED", recordId, oldMedicine, model,
                    $"Medicine record update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("IX_MedMaster_MedItemNameBaseIdCompanyName_Unique") == true)
                {
                    ModelState.AddModelError("", "A medical item with this combination already exists in your plant. Please choose different values.");
                    ModelState.AddModelError("MedItemName", "This combination already exists in your plant.");
                    ModelState.AddModelError("BaseId", "This combination already exists in your plant.");
                    ModelState.AddModelError("CompanyName", "This combination already exists in your plant.");
                    ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(await GetCurrentUserPlantIdAsync()), "BaseId", "BaseName", model.BaseId);
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the medicine. Please try again.";
                }

                ViewBag.MedBaseList = new SelectList(await _repo.GetBaseListAsync(await GetCurrentUserPlantIdAsync()), "BaseId", "BaseName", model.BaseId);
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedMaster? medicineToDelete = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get entity before deletion for audit and plant validation
                medicineToDelete = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (medicineToDelete == null)
                {
                    await _auditService.LogAsync("med_master", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent medicine record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return Json(new { success = false, message = "Medicine record not found or access denied." });
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && medicineToDelete.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("med_master", "DELETE_PLANT_DENY", id.ToString(), medicineToDelete, null,
                        $"Delete denied - record belongs to different plant: {medicineToDelete.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only delete records from your assigned plant." });
                }

                var baseName = medicineToDelete.MedBase?.BaseName ?? "Unknown";
                await _auditService.LogAsync("med_master", "DELETE_ATTEMPT", id.ToString(), medicineToDelete, null,
                    $"Medicine record deletion attempt for: {medicineToDelete.MedItemName} (Base: {baseName}, Company: {medicineToDelete.CompanyName}) in plant: {medicineToDelete.plant_id}");

                await _repo.DeleteAsync(id, userPlantId);

                await _auditService.LogDeleteAsync("med_master", id.ToString(), medicineToDelete,
                    $"Medicine record '{medicineToDelete.MedItemName}' (Base: {baseName}, Company: {medicineToDelete.CompanyName}) deleted successfully from plant: {medicineToDelete.plant_id}");

                return Json(new { success = true, message = "Medicine record deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "DELETE_FAILED", id.ToString(), medicineToDelete, null,
                    $"Medicine record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the medicine." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                var item = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("med_master", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent medicine record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                var baseName = item.MedBase?.BaseName ?? "Unknown";
                await _auditService.LogViewAsync("med_master", id.ToString(),
                    $"Medicine record details viewed: {item.MedItemName} (Base: {baseName}, Company: {item.CompanyName}) in plant: {item.OrgPlant?.plant_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_master", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading medicine record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation with plant filtering
        [HttpPost]
        public async Task<IActionResult> CheckMedItemDetailsExists(string medItemName, int? baseId, string? companyName, int? medItemId = null)
        {
            if (string.IsNullOrWhiteSpace(medItemName))
                return Json(new { exists = false });

            // Sanitize input before checking
            medItemName = SanitizeString(medItemName);
            companyName = SanitizeString(companyName);

            var userPlantId = await GetCurrentUserPlantIdAsync();
            var exists = await _repo.IsMedItemDetailsExistsAsync(medItemName, baseId, companyName, medItemId, userPlantId);
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
                await _auditService.LogAsync("med_master", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        #region Private Methods for Input Sanitization and Validation

        private MedMaster SanitizeInput(MedMaster model)
        {
            if (model == null) return model;

            model.MedItemName = SanitizeString(model.MedItemName);
            model.CompanyName = SanitizeString(model.CompanyName);

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

        private bool IsInputSecure(MedMaster model)
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

            var inputsToCheck = new[] { model.MedItemName, model.CompanyName };

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