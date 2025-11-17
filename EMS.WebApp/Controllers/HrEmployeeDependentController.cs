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
    [Authorize("AccessHrEmployeeDependent")]
    public class HrEmployeeDependentController : Controller
    {
        private readonly IHrEmployeeDependentRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public HrEmployeeDependentController(IHrEmployeeDependentRepository repo, IMemoryCache cache, IAuditService auditService)
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

                await _auditService.LogAsync("hr_employee_dependent", "INDEX_VIEW", "main", null, null,
                    $"Employee dependent module accessed by user, Plant: {userPlantId}");
                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "INDEX_FAILED", "main", null, null,
                    $"Failed to load employee dependent index: {ex.Message}");
                throw;
            }
        }

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                await _auditService.LogAsync("hr_employee_dependent", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - User: {currentUser}, Plant: {userPlantId}");

                var list = await _repo.ListWithBaseAsync(userPlantId);

                var result = list.Select(x => new
                {
                    x.emp_dep_id,
                    emp_name = x.HrEmployee != null ? x.HrEmployee.emp_name : "",
                    x.dep_name,
                    x.dep_dob,
                    x.relation,
                    x.gender,
                    x.is_active,
                    x.marital_status,
                    x.CreatedBy,
                    x.CreatedOn,
                    x.ModifiedBy,
                    x.ModifiedOn,
                    age = x.Age,
                    isOverAgeLimit = x.IsChildOverAgeLimit,
                    PlantName = x.OrgPlant?.plant_name ?? "Unknown Plant"
                });

                await _auditService.LogAsync("hr_employee_dependent", "LOAD_DATA_SUCCESS", "multiple", null, null,
                    $"Loaded {list.Count()} employee dependent records for listing, Plant: {userPlantId}");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load employee dependent records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("hr_employee_dependent", "CREATE_FORM_VIEW", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_NO_PLANT", "new", null, null,
                        "Create failed - user has no plant assigned");
                    ViewBag.EmpDependentList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    return PartialView("_CreateEdit", new HrEmployeeDependent());
                }

                var empDependent = await _repo.GetBaseListAsync(userPlantId); // Returns only married employees from user's plant

                if (!empDependent.Any())
                {
                    ViewBag.EmpDependentList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "⚠ No married employees found in your plant! Only married employees can have dependents.";
                }
                else
                {
                    ViewBag.EmpDependentList = new SelectList(empDependent, "emp_uid", "emp_name");
                }

                var model = new HrEmployeeDependent
                {
                    plant_id = (short)userPlantId.Value
                };

                await _auditService.LogAsync("hr_employee_dependent", "CREATE_FORM_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "CREATE_FORM_ERROR", "new", null, null,
                    $"Error loading create form: {ex.Message}");

                ViewBag.Error = "Error loading employee list.";
                return PartialView("_CreateEdit", new HrEmployeeDependent());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HrEmployeeDependent model)
        {
            string recordId = "new";

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.emp_dep_id.ToString();

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                model.plant_id = (short)userPlantId.Value;

                // Security check: Ensure selected employee belongs to user's plant
                if (!await _repo.IsEmployeeInUserPlantAsync(model.emp_uid, userPlantId.Value))
                {
                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_EMPLOYEE_PLANT_DENY", recordId, null, model,
                        $"Create denied - selected employee {model.emp_uid} does not belong to user plant: {userPlantId}");
                    ViewBag.Error = "Selected employee does not belong to your plant. Please refresh and try again.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name");
                    return PartialView("_CreateEdit", model);
                }

                await _auditService.LogAsync("hr_employee_dependent", "CREATE_ATTEMPT", recordId, null, model,
                    $"Employee dependent record creation attempt started for plant: {model.plant_id}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during employee dependent record creation");

                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Remove plant_id from ModelState validation as it's set programmatically
                ModelState.Remove("plant_id");

                // Business Rule Validations
                var businessValidationResult = await ValidateBusinessRules(model, userPlantId);
                if (!businessValidationResult.IsValid)
                {
                    ModelState.AddModelError("", businessValidationResult.ErrorMessage);

                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_BUSINESS_RULE_VIOLATION", recordId, null, model,
                        $"Business rule violation: {businessValidationResult.ErrorMessage}");

                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Validate date of birth
                if (model.dep_dob.HasValue)
                {
                    var dobDate = model.dep_dob.Value.ToDateTime(TimeOnly.MinValue);
                    var today = DateTime.Now;
                    var age = today.Year - dobDate.Year;
                    if (dobDate > today.AddYears(-age)) age--;

                    if (dobDate > today)
                    {
                        ModelState.AddModelError("dep_dob", "Date of Birth cannot be in the future.");

                        await _auditService.LogAsync("hr_employee_dependent", "CREATE_INVALID_DOB_FUTURE", recordId, null, model,
                            $"Invalid date of birth in future: {model.dep_dob}");
                    }
                    else if (age > 100)
                    {
                        ModelState.AddModelError("dep_dob", "Age cannot exceed 100 years.");

                        await _auditService.LogAsync("hr_employee_dependent", "CREATE_INVALID_AGE_TOO_OLD", recordId, null, model,
                            $"Invalid age over 100 years: {age}");
                    }
                    else if (model.relation?.ToLower() == "child" && age > 21)
                    {
                        ModelState.AddModelError("dep_dob", "Child dependents cannot be older than 21 years.");

                        await _auditService.LogAsync("hr_employee_dependent", "CREATE_INVALID_CHILD_AGE", recordId, null, model,
                            $"Invalid child age over 21 years: {age}");
                    }
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_empdependent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 dependents every 5 minutes. Please wait and try again.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
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
                recordId = model.emp_dep_id.ToString();

                await _auditService.LogCreateAsync("hr_employee_dependent", recordId, model,
                    $"Employee dependent '{model.dep_name}' (Relation: {model.relation}) created successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Employee dependent created successfully!", empDepId = model.emp_dep_id });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "CREATE_FAILED", recordId, null, model,
                    $"Employee dependent record creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
                {
                    ViewBag.Error = "Plant or employee assignment error. Please contact administrator.";
                }
                else if (ex.InnerException?.Message.Contains("plant_id") == true)
                {
                    ViewBag.Error = "Invalid plant assignment. Please refresh and try again.";
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the dependent record. Please try again.";
                }

                ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(await GetCurrentUserPlantIdAsync()), "emp_uid", "emp_name", model.emp_uid);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("hr_employee_dependent", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed for plant: {userPlantId}");

                var item = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent employee dependent record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && item.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "EDIT_PLANT_DENY", id.ToString(), item, null,
                        $"Edit denied - record belongs to different plant: {item.plant_id} vs user plant: {userPlantId}");
                    return NotFound();
                }

                await _auditService.LogViewAsync("hr_employee_dependent", id.ToString(),
                    $"Edit form accessed for employee dependent: {item.dep_name} (Relation: {item.relation}) in plant: {item.OrgPlant?.plant_name}");

                ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", item.emp_uid);
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HrEmployeeDependent model)
        {
            var recordId = model.emp_dep_id.ToString();
            HrEmployeeDependent? oldDependent = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get the current dependent for audit comparison and plant validation
                oldDependent = await _repo.GetByIdWithBaseAsync(model.emp_dep_id, userPlantId);
                if (oldDependent == null)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "EDIT_NOT_FOUND", recordId, null, model,
                        $"Attempted to edit non-existent employee dependent record or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && oldDependent.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "EDIT_PLANT_DENY", recordId, oldDependent, model,
                        $"Edit denied - record belongs to different plant: {oldDependent.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only edit records from your assigned plant." });
                }

                // Security check: Ensure selected employee belongs to user's plant
                if (!await _repo.IsEmployeeInUserPlantAsync(model.emp_uid, userPlantId.Value))
                {
                    await _auditService.LogAsync("hr_employee_dependent", "UPDATE_EMPLOYEE_PLANT_DENY", recordId, oldDependent, model,
                        $"Update denied - selected employee {model.emp_uid} does not belong to user plant: {userPlantId}");
                    ViewBag.Error = "Selected employee does not belong to your plant. Please refresh and try again.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                await _auditService.LogAsync("hr_employee_dependent", "UPDATE_ATTEMPT", recordId, oldDependent, model,
                    $"Employee dependent record update attempt for: {oldDependent.dep_name} (Relation: {oldDependent.relation}) in plant: {oldDependent.plant_id}");

                // Preserve plant_id from original record (don't allow changing plant)
                model.plant_id = oldDependent.plant_id;

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("hr_employee_dependent", "UPDATE_SECURITY_VIOLATION", recordId, oldDependent, model,
                        "Insecure input detected during employee dependent record update");

                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Business Rule Validations (exclude current dependent from limit check)
                var businessValidationResult = await ValidateBusinessRules(model, userPlantId, model.emp_dep_id);
                if (!businessValidationResult.IsValid)
                {
                    ModelState.AddModelError("", businessValidationResult.ErrorMessage);

                    await _auditService.LogAsync("hr_employee_dependent", "UPDATE_BUSINESS_RULE_VIOLATION", recordId, oldDependent, model,
                        $"Business rule violation: {businessValidationResult.ErrorMessage}");

                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Validate date of birth
                if (model.dep_dob.HasValue)
                {
                    var dobDate = model.dep_dob.Value.ToDateTime(TimeOnly.MinValue);
                    var today = DateTime.Now;
                    var age = today.Year - dobDate.Year;
                    if (dobDate > today.AddYears(-age)) age--;

                    if (dobDate > today)
                    {
                        ModelState.AddModelError("dep_dob", "Date of Birth cannot be in the future.");

                        await _auditService.LogAsync("hr_employee_dependent", "UPDATE_INVALID_DOB_FUTURE", recordId, oldDependent, model,
                            $"Invalid date of birth in future: {model.dep_dob}");
                    }
                    else if (age > 100)
                    {
                        ModelState.AddModelError("dep_dob", "Age cannot exceed 100 years.");

                        await _auditService.LogAsync("hr_employee_dependent", "UPDATE_INVALID_AGE_TOO_OLD", recordId, oldDependent, model,
                            $"Invalid age over 100 years: {age}");
                    }
                    else if (model.relation?.ToLower() == "child" && age > 21)
                    {
                        ModelState.AddModelError("dep_dob", "Child dependents cannot be older than 21 years.");

                        await _auditService.LogAsync("hr_employee_dependent", "UPDATE_INVALID_CHILD_AGE", recordId, oldDependent, model,
                            $"Invalid child age over 21 years: {age}");
                    }
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("hr_employee_dependent", "UPDATE_VALIDATION_FAILED", recordId, oldDependent, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_empdependent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "UPDATE_RATE_LIMITED", recordId, oldDependent, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 dependents every 5 minutes. Please wait and try again.";
                    ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(userPlantId), "emp_uid", "emp_name", model.emp_uid);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                await _auditService.LogUpdateAsync("hr_employee_dependent", recordId, oldDependent, model,
                    $"Employee dependent '{model.dep_name}' (Relation: {model.relation}) updated successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Employee dependent updated successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "UPDATE_FAILED", recordId, oldDependent, model,
                    $"Employee dependent record update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the dependent record. Please try again.";
                }

                ViewBag.EmpDependentList = new SelectList(await _repo.GetBaseListAsync(await GetCurrentUserPlantIdAsync()), "emp_uid", "emp_name", model.emp_uid);
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            HrEmployeeDependent? dependentToDelete = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get entity before deletion for audit and plant validation
                dependentToDelete = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (dependentToDelete == null)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent employee dependent record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return Json(new { success = false, message = "Employee dependent record not found or access denied." });
                }

                // Security check: Ensure the record belongs to user's plant
                if (userPlantId.HasValue && dependentToDelete.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee_dependent", "DELETE_PLANT_DENY", id.ToString(), dependentToDelete, null,
                        $"Delete denied - record belongs to different plant: {dependentToDelete.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only delete records from your assigned plant." });
                }

                await _auditService.LogAsync("hr_employee_dependent", "DELETE_ATTEMPT", id.ToString(), dependentToDelete, null,
                    $"Employee dependent record deletion attempt for: {dependentToDelete.dep_name} (Relation: {dependentToDelete.relation}) in plant: {dependentToDelete.plant_id}");

                await _repo.DeleteAsync(id, userPlantId);

                await _auditService.LogDeleteAsync("hr_employee_dependent", id.ToString(), dependentToDelete,
                    $"Employee dependent '{dependentToDelete.dep_name}' (Relation: {dependentToDelete.relation}) deleted successfully from plant: {dependentToDelete.plant_id}");

                return Json(new { success = true, message = "Employee dependent deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "DELETE_FAILED", id.ToString(), dependentToDelete, null,
                    $"Employee dependent record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the dependent record." });
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
                    await _auditService.LogAsync("hr_employee_dependent", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent employee dependent record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                await _auditService.LogViewAsync("hr_employee_dependent", id.ToString(),
                    $"Employee dependent record details viewed: {item.dep_name} (Relation: {item.relation}) in plant: {item.OrgPlant?.plant_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading employee dependent record details: {ex.Message}");

                return NotFound();
            }
        }

        // Updated action to check and deactivate children over age limit with plant filtering
        [HttpPost]
        public async Task<IActionResult> DeactivateOverAgeDependents()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("hr_employee_dependent", "BULK_DEACTIVATE_ATTEMPT", "multiple", null, null,
                    $"Bulk deactivation of over-age dependents attempted for plant: {userPlantId}");

                var deactivatedCount = await _repo.DeactivateChildrenOverAgeLimitAsync(userPlantId);

                await _auditService.LogAsync("hr_employee_dependent", "BULK_DEACTIVATE_SUCCESS", "multiple", null, null,
                    $"Successfully deactivated {deactivatedCount} child dependents over age 21 in plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    message = $"{deactivatedCount} child dependents over age 21 have been deactivated."
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "BULK_DEACTIVATE_FAILED", "multiple", null, null,
                    $"Bulk deactivation of over-age dependents failed: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "An error occurred while processing age limit check."
                });
            }
        }

        // Updated action to get dependents by employee (for AJAX calls) with plant filtering
        [HttpGet]
        public async Task<IActionResult> GetEmployeeDependents(int empUid)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Security check: Ensure employee belongs to user's plant
                if (userPlantId.HasValue && !await _repo.IsEmployeeInUserPlantAsync(empUid, userPlantId.Value))
                {
                    await _auditService.LogAsync("hr_employee_dependent", "EMPLOYEE_DEPENDENTS_PLANT_DENY", empUid.ToString(), null, null,
                        $"Employee dependents access denied - employee {empUid} does not belong to user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. Employee does not belong to your plant." });
                }

                await _auditService.LogAsync("hr_employee_dependent", "EMPLOYEE_DEPENDENTS_ACCESS", empUid.ToString(), null, null,
                    $"Employee dependents accessed for employee UID: {empUid} in plant: {userPlantId}");

                var dependents = await _repo.GetActiveDependentsByEmployeeAsync(empUid, userPlantId);
                var result = dependents.Select(d => new
                {
                    d.emp_dep_id,
                    d.dep_name,
                    d.relation,
                    d.Age,
                    isOverAgeLimit = d.IsChildOverAgeLimit
                });

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee_dependent", "EMPLOYEE_DEPENDENTS_ERROR", empUid.ToString(), null, null,
                    $"Error loading employee dependents for UID {empUid}: {ex.Message}");

                return Json(new { success = false, message = "Error loading employee dependents." });
            }
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
                await _auditService.LogAsync("hr_employee_dependent", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        #region Private Methods for Business Rule Validation

        private async Task<BusinessValidationResult> ValidateBusinessRules(HrEmployeeDependent model, int? userPlantId, int? excludeDependentId = null)
        {
            var result = new BusinessValidationResult { IsValid = true };

            // Check if relation is allowed
            var allowedRelations = new[] { "Wife", "Husband", "Child" };
            if (!allowedRelations.Contains(model.relation, StringComparer.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.ErrorMessage = "Only Wife, Husband, and Child relations are allowed. Parents are not permitted as dependents.";
                return result;
            }

            // Check dependent limits with plant filtering
            var limitValidation = await _repo.ValidateDependentLimitsAsync(model.emp_uid, model.relation, excludeDependentId, userPlantId);
            if (!limitValidation.IsValid)
            {
                result.IsValid = false;
                result.ErrorMessage = limitValidation.ErrorMessage;
                return result;
            }

            return result;
        }

        #endregion

        #region Private Methods for Input Sanitization and Validation

        private HrEmployeeDependent SanitizeInput(HrEmployeeDependent model)
        {
            if (model == null) return model;

            model.dep_name = SanitizeString(model.dep_name);
            model.relation = SanitizeString(model.relation);

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

        private bool IsInputSecure(HrEmployeeDependent model)
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

            var inputsToCheck = new[] { model.dep_name, model.relation };

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

    // Helper class for business validation results
    public class BusinessValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}