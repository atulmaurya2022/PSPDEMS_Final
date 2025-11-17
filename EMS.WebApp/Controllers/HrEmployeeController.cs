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
    [Authorize("AccessHrEmployee")]
    public class HrEmployeeController : Controller
    {
        private readonly IHrEmployeeRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public HrEmployeeController(IHrEmployeeRepository repo, IMemoryCache cache, IAuditService auditService)
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

                await _auditService.LogAsync("hr_employee", "INDEX_VIEW", "main", null, null,
                    $"HR employee module accessed by user, Plant: {userPlantId}");
                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "INDEX_FAILED", "main", null, null,
                    $"Failed to load HR employee index: {ex.Message}");
                throw;
            }
        }

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                await _auditService.LogAsync("hr_employee", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - User: {currentUser}, Plant: {userPlantId}");

                var list = await _repo.ListWithBaseAsync(userPlantId);

                var result = list.Select(x => new
                {
                    x.emp_uid,
                    x.emp_id,
                    x.emp_name,
                    emp_DOB = x.emp_DOB?.ToString("dd/MM/yyyy"),
                    x.emp_Gender,
                    x.emp_Grade,
                    dept_name = x.org_department != null ? x.org_department.dept_name : "",
                    plant_name = x.org_plant != null ? x.org_plant.plant_name : "",
                    emp_category_name = x.org_employee_category != null ? x.org_employee_category.emp_category_name : "",
                    x.emp_blood_Group,
                    marital_status = x.marital_status.HasValue ? (x.marital_status.Value ? "Married" : "Single") : "Not Specified",
                    x.CreatedBy,
                    CreatedOn = x.CreatedOn?.ToString("dd/MM/yyyy HH:mm"),
                    x.ModifiedBy,
                    ModifiedOn = x.ModifiedOn?.ToString("dd/MM/yyyy HH:mm")
                }).ToList();

                await _auditService.LogAsync("hr_employee", "LOAD_DATA_SUCCESS", "multiple", null, null,
                    $"Loaded {result.Count} employee records for listing, Plant: {userPlantId}");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load employee records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("hr_employee", "CREATE_FORM_VIEW", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("hr_employee", "CREATE_NO_PLANT", "new", null, null,
                        "Create failed - user has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                var departmentList = await _repo.GetDepartmentListAsync(userPlantId);
                var plantList = await _repo.GetPlantListAsync(userPlantId);
                var categoryList = await _repo.GetEmployeeCategoryListAsync(userPlantId);

                ViewBag.OrgDepartmentList = new SelectList(departmentList, "dept_id", "dept_name");
                ViewBag.OrgPlantList = new SelectList(plantList, "plant_id", "plant_name");
                ViewBag.OrgEmployeeCategoryList = new SelectList(categoryList, "emp_category_id", "emp_category_name");

                var model = new HrEmployee
                {
                    plant_id = (short)userPlantId.Value // Pre-assign user's plant
                };

                await _auditService.LogAsync("hr_employee", "CREATE_FORM_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "CREATE_FORM_ERROR", "new", null, null,
                    $"Create form error: {ex.Message}");

                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new HrEmployee());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HrEmployee model)
        {
            string recordId = "new";

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.emp_uid.ToString();

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("hr_employee", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    return PartialView("_CreateEdit", model);
                }

                // Security check: Ensure employee is being assigned to user's plant
                if (model.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee", "CREATE_PLANT_DENY", recordId, null, model,
                        $"Create denied - trying to assign to different plant: {model.plant_id} vs user plant: {userPlantId}");
                    ViewBag.Error = "You can only create employees for your assigned plant.";
                    return PartialView("_CreateEdit", model);
                }

                await _auditService.LogAsync("hr_employee", "CREATE_ATTEMPT", recordId, null, model,
                    $"Employee record creation attempt started for plant: {model.plant_id}");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("hr_employee", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during employee record creation");

                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate employee ID within the same plant
                if (await _repo.IsEmployeeIdExistsAsync(model.emp_id, null, userPlantId))
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists in your plant. Please choose a different ID.");

                    await _auditService.LogAsync("hr_employee", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate employee ID in plant {userPlantId}: {model.emp_id}");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("hr_employee", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_hremployee_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("hr_employee", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 employees every 5 minutes. Please wait and try again.";
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", model.emp_category_id);
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
                recordId = model.emp_uid.ToString();

                // Get department, plant and category names for better audit message
                var departmentList = await _repo.GetDepartmentListAsync(userPlantId);
                var plantList = await _repo.GetPlantListAsync(userPlantId);
                var categoryList = await _repo.GetEmployeeCategoryListAsync(userPlantId);
                var deptName = departmentList.FirstOrDefault(d => d.dept_id == model.dept_id)?.dept_name ?? "Unknown";
                var plantName = plantList.FirstOrDefault(p => p.plant_id == model.plant_id)?.plant_name ?? "Unknown";
                var categoryName = categoryList.FirstOrDefault(c => c.emp_category_id == model.emp_category_id)?.emp_category_name ?? "Unknown";

                await _auditService.LogCreateAsync("hr_employee", recordId, model,
                    $"Employee '{model.emp_name}' (ID: {model.emp_id}, Dept: {deptName}, Plant: {plantName}, Category: {categoryName}) created successfully in plant: {model.plant_id}");

                return Json(new { success = true, message = "Employee created successfully!", empUid = model.emp_uid });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "CREATE_FAILED", recordId, null, model,
                    $"Employee record creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
                {
                    ViewBag.Error = "Invalid department, plant, or category selection. Please refresh and try again.";
                }
                else if (ex.InnerException?.Message.Contains("IX_HrEmployee_EmpId_Unique") == true)
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists in your plant. Please choose a different ID.");
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(await GetCurrentUserPlantIdAsync()), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(await GetCurrentUserPlantIdAsync()), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(await GetCurrentUserPlantIdAsync()), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the employee. Please try again.";
                }

                ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(await GetCurrentUserPlantIdAsync()), "dept_id", "dept_name", model.dept_id);
                ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(await GetCurrentUserPlantIdAsync()), "plant_id", "plant_name", model.plant_id);
                ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(await GetCurrentUserPlantIdAsync()), "emp_category_id", "emp_category_name", model.emp_category_id);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("hr_employee", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed for plant: {userPlantId}");

                var item = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("hr_employee", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent employee record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                var deptName = item.org_department?.dept_name ?? "Unknown";
                var plantName = item.org_plant?.plant_name ?? "Unknown";
                var categoryName = item.org_employee_category?.emp_category_name ?? "Unknown";
                await _auditService.LogViewAsync("hr_employee", id.ToString(),
                    $"Edit form accessed for employee: {item.emp_name} (ID: {item.emp_id}, Dept: {deptName}, Plant: {plantName}, Category: {categoryName})");

                ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", item.dept_id);
                ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", item.plant_id);
                ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", item.emp_category_id);

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HrEmployee model)
        {
            var recordId = model.emp_uid.ToString();
            HrEmployee? oldEmployee = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get the current employee for audit comparison and plant validation
                oldEmployee = await _repo.GetByIdWithBaseAsync(model.emp_uid, userPlantId);
                if (oldEmployee == null)
                {
                    await _auditService.LogAsync("hr_employee", "EDIT_NOT_FOUND", recordId, null, model,
                        $"Attempted to edit non-existent employee record or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                // Security check: Ensure the employee belongs to user's plant
                if (userPlantId.HasValue && oldEmployee.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee", "EDIT_PLANT_DENY", recordId, oldEmployee, model,
                        $"Edit denied - employee belongs to different plant: {oldEmployee.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only edit employees from your assigned plant." });
                }

                // Additional security check for plant assignment change
                if (model.plant_id != oldEmployee.plant_id && model.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee", "EDIT_TRANSFER_DENY", recordId, oldEmployee, model,
                        $"Edit denied - cannot transfer to different plant: {model.plant_id} (user plant: {userPlantId})");
                    return Json(new { success = false, message = "You cannot transfer employees to plants other than your own." });
                }

                var oldDeptName = oldEmployee.org_department?.dept_name ?? "Unknown";
                var oldPlantName = oldEmployee.org_plant?.plant_name ?? "Unknown";
                var oldCategoryName = oldEmployee.org_employee_category?.emp_category_name ?? "Unknown";
                await _auditService.LogAsync("hr_employee", "UPDATE_ATTEMPT", recordId, oldEmployee, model,
                    $"Employee record update attempt for: {oldEmployee.emp_name} (ID: {oldEmployee.emp_id}, Dept: {oldDeptName}, Plant: {oldPlantName}, Category: {oldCategoryName})");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    await _auditService.LogAsync("hr_employee", "UPDATE_SECURITY_VIOLATION", recordId, oldEmployee, model,
                        "Insecure input detected during employee record update");

                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate employee ID within the same plant (excluding current record)
                if (await _repo.IsEmployeeIdExistsAsync(model.emp_id, model.emp_uid, userPlantId))
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists in your plant. Please choose a different ID.");

                    await _auditService.LogAsync("hr_employee", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldEmployee, model,
                        $"Attempted to update to duplicate employee ID in plant {userPlantId}: {model.emp_id}");
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("hr_employee", "UPDATE_VALIDATION_FAILED", recordId, oldEmployee, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_hremployee_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("hr_employee", "UPDATE_RATE_LIMITED", recordId, oldEmployee, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 employees every 5 minutes. Please wait and try again.";
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(userPlantId), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(userPlantId), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(userPlantId), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Get updated department, plant and category names for better audit message
                var departmentList = await _repo.GetDepartmentListAsync(userPlantId);
                var plantList = await _repo.GetPlantListAsync(userPlantId);
                var categoryList = await _repo.GetEmployeeCategoryListAsync(userPlantId);
                var updatedDeptName = departmentList.FirstOrDefault(d => d.dept_id == model.dept_id)?.dept_name ?? "Unknown";
                var updatedPlantName = plantList.FirstOrDefault(p => p.plant_id == model.plant_id)?.plant_name ?? "Unknown";
                var updatedCategoryName = categoryList.FirstOrDefault(c => c.emp_category_id == model.emp_category_id)?.emp_category_name ?? "Unknown";

                await _auditService.LogUpdateAsync("hr_employee", recordId, oldEmployee, model,
                    $"Employee '{model.emp_name}' (ID: {model.emp_id}, Dept: {updatedDeptName}, Plant: {updatedPlantName}, Category: {updatedCategoryName}) updated successfully");

                return Json(new { success = true, message = "Employee updated successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "UPDATE_FAILED", recordId, oldEmployee, model,
                    $"Employee record update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("IX_HrEmployee_EmpId_Unique") == true)
                {
                    ModelState.AddModelError("emp_id", "An employee with this ID already exists in your plant. Please choose a different ID.");
                    ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(await GetCurrentUserPlantIdAsync()), "dept_id", "dept_name", model.dept_id);
                    ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(await GetCurrentUserPlantIdAsync()), "plant_id", "plant_name", model.plant_id);
                    ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(await GetCurrentUserPlantIdAsync()), "emp_category_id", "emp_category_name", model.emp_category_id);
                    return PartialView("_CreateEdit", model);
                }
                else if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A database constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the employee. Please try again.";
                }

                ViewBag.OrgDepartmentList = new SelectList(await _repo.GetDepartmentListAsync(await GetCurrentUserPlantIdAsync()), "dept_id", "dept_name", model.dept_id);
                ViewBag.OrgPlantList = new SelectList(await _repo.GetPlantListAsync(await GetCurrentUserPlantIdAsync()), "plant_id", "plant_name", model.plant_id);
                ViewBag.OrgEmployeeCategoryList = new SelectList(await _repo.GetEmployeeCategoryListAsync(await GetCurrentUserPlantIdAsync()), "emp_category_id", "emp_category_name", model.emp_category_id);
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            HrEmployee? employee = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get entity before deletion for audit and plant validation
                employee = await _repo.GetByIdWithBaseAsync(id, userPlantId);
                if (employee == null)
                {
                    await _auditService.LogAsync("hr_employee", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent employee record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return Json(new { success = false, message = "Employee record not found or access denied." });
                }

                // Security check: Ensure the employee belongs to user's plant
                if (userPlantId.HasValue && employee.plant_id != userPlantId.Value)
                {
                    await _auditService.LogAsync("hr_employee", "DELETE_PLANT_DENY", id.ToString(), employee, null,
                        $"Delete denied - employee belongs to different plant: {employee.plant_id} vs user plant: {userPlantId}");
                    return Json(new { success = false, message = "Access denied. You can only delete employees from your assigned plant." });
                }

                var deptName = employee.org_department?.dept_name ?? "Unknown";
                var plantName = employee.org_plant?.plant_name ?? "Unknown";
                var categoryName = employee.org_employee_category?.emp_category_name ?? "Unknown";
                await _auditService.LogAsync("hr_employee", "DELETE_ATTEMPT", id.ToString(), employee, null,
                    $"Employee record deletion attempt for: {employee.emp_name} (ID: {employee.emp_id}, Dept: {deptName}, Plant: {plantName}, Category: {categoryName})");

                await _repo.DeleteAsync(id, userPlantId);

                await _auditService.LogDeleteAsync("hr_employee", id.ToString(), employee,
                    $"Employee '{employee.emp_name}' (ID: {employee.emp_id}, Dept: {deptName}, Plant: {plantName}, Category: {categoryName}) deleted successfully from plant: {employee.plant_id}");

                return Json(new { success = true, message = "Employee deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "DELETE_FAILED", id.ToString(), employee, null,
                    $"Employee record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the employee." });
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
                    await _auditService.LogAsync("hr_employee", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent employee record with ID: {id} or unauthorized access for plant: {userPlantId}");

                    return NotFound();
                }

                var deptName = item.org_department?.dept_name ?? "Unknown";
                var plantName = item.org_plant?.plant_name ?? "Unknown";
                var categoryName = item.org_employee_category?.emp_category_name ?? "Unknown";
                await _auditService.LogViewAsync("hr_employee", id.ToString(),
                    $"Employee record details viewed: {item.emp_name} (ID: {item.emp_id}, Dept: {deptName}, Plant: {plantName}, Category: {categoryName})");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("hr_employee", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading employee record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation with plant filtering
        [HttpPost]
        public async Task<IActionResult> CheckEmployeeIdExists(string empId, int? empUid = null)
        {
            if (string.IsNullOrWhiteSpace(empId))
                return Json(new { exists = false });

            // Sanitize input before checking
            empId = SanitizeString(empId);

            var userPlantId = await GetCurrentUserPlantIdAsync();
            var exists = await _repo.IsEmployeeIdExistsAsync(empId, empUid, userPlantId);
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
                await _auditService.LogAsync("hr_employee", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        #region Private Methods for Input Sanitization and Validation

        private HrEmployee SanitizeInput(HrEmployee model)
        {
            if (model == null) return model;

            model.emp_id = SanitizeString(model.emp_id);
            model.emp_name = SanitizeString(model.emp_name);
            model.emp_Grade = SanitizeString(model.emp_Grade);
            model.emp_blood_Group = SanitizeString(model.emp_blood_Group);

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

        private bool IsInputSecure(HrEmployee model)
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

            var inputsToCheck = new[] { model.emp_id, model.emp_name, model.emp_Grade, model.emp_blood_Group };

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