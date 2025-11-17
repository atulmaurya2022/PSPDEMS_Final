using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedExaminationResult")]
    public class MedExaminationResultController : Controller
    {
        private readonly IMedExaminationResultRepository _medExamResultRepository;
        private readonly ILogger<MedExaminationResultController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public MedExaminationResultController(
            IMedExaminationResultRepository medExamResultRepository,
            ILogger<MedExaminationResultController> logger,
            ApplicationDbContext db,
            IAuditService auditService)
        {
            _medExamResultRepository = medExamResultRepository;
            _logger = logger;
            _db = db;
            _auditService = auditService;
        }

        // Helper method to get current user's plant ID
        private async Task<int?> GetCurrentUserPlantIdAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                return await _medExamResultRepository.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user plant ID");
                await _auditService.LogAsync("med_exam_result", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        // Helper method to get current username
        private string GetCurrentUsername()
        {
            return User.Identity?.Name + " - " + User.GetFullName() ?? "System";
        }

        // GET: Index - List all medical examination results
        public async Task<IActionResult> Index()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Medical Examination Result Index accessed for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "INDEX_VIEW", "main", null, null,
                    $"Medical examination result list accessed - Plant: {userPlantId}");

                var model = new MedExaminationResultListViewModel();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Medical Examination Result Index page");
                await _auditService.LogAsync("med_exam_result", "INDEX_FAILED", "main", null, null,
                    $"Failed to load medical examination result index: {ex.Message}");
                return View(new MedExaminationResultListViewModel());
            }
        }

        // GET: GetResultsList - AJAX endpoint to get list of results
        [HttpGet]
        public async Task<IActionResult> GetResultsList(string searchTerm = "")
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUsername = GetCurrentUsername();

                _logger.LogInformation($"Getting results list for plant: {userPlantId}, search: {searchTerm}");

                await _auditService.LogAsync("med_exam_result", "GET_LIST", "bulk", null, null,
                    $"Results list requested - SearchTerm: {searchTerm ?? "none"}, Plant: {userPlantId}");

                var results = await _medExamResultRepository.GetResultsListAsync(userPlantId, searchTerm, currentUsername);

                await _auditService.LogAsync("med_exam_result", "LIST_SUCCESS", "bulk", null, null,
                    $"Results list loaded - Count: {results.Count()}, Plant: {userPlantId}");

                return Json(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting results list");
                await _auditService.LogAsync("med_exam_result", "LIST_FAILED", "bulk", null, null,
                    $"Failed to get results list: {ex.Message}");
                return Json(new List<MedExaminationResultListItemViewModel>());
            }
        }

        // GET: Create - Show create form
        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Medical Examination Result Create accessed for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "CREATE_FORM", "main", null, null,
                    $"Medical exam result create form accessed - Plant: {userPlantId}");

                var model = new MedExaminationResultViewModel
                {
                    ExamCategories = await _medExamResultRepository.GetExamCategoriesAsync(),
                    TestLocations = await _medExamResultRepository.GetTestLocationsAsync()
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Medical Examination Result Create page");
                await _auditService.LogAsync("med_exam_result", "CREATE_FAILED", "main", null, null,
                    $"Failed to load medical exam result create form: {ex.Message}");
                return View(new MedExaminationResultViewModel());
            }
        }

        // GET: View - View medical examination result details
        [HttpGet]
        public async Task<IActionResult> View(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUsername = GetCurrentUsername();

                _logger.LogInformation($"Viewing medical exam result {id} for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "VIEW_ATTEMPT", id.ToString(), null, null,
                    $"Medical exam result view attempted - ID: {id}, Plant: {userPlantId}");

                var model = await _medExamResultRepository.GetResultForViewAsync(id, userPlantId, currentUsername);

                if (model == null)
                {
                    _logger.LogWarning($"Medical exam result {id} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("med_exam_result", "VIEW_NOTFOUND", id.ToString(), null, null,
                        $"Medical exam result not found or access denied - ID: {id}, Plant: {userPlantId}");
                    return NotFound("Medical examination result not found or access denied.");
                }

                await _auditService.LogViewAsync("med_exam_result", id.ToString(),
                    $"Medical exam result viewed - Employee: {model.EmployeeName}, Category: {model.CategoryName}, Plant: {userPlantId}");

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error viewing medical exam result {id}");
                await _auditService.LogAsync("med_exam_result", "VIEW_ERROR", id.ToString(), null, null,
                    $"Error viewing medical exam result: {ex.Message}");
                return BadRequest($"Error viewing medical exam result: {ex.Message}");
            }
        }

        // GET: Edit - Show edit form
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUsername = GetCurrentUsername();

                _logger.LogInformation($"Editing medical exam result {id} for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "EDIT_ATTEMPT", id.ToString(), null, null,
                    $"Medical exam result edit form access attempted - ID: {id}, Plant: {userPlantId}");

                var model = await _medExamResultRepository.GetResultForEditAsync(id, userPlantId, currentUsername);

                if (model == null)
                {
                    _logger.LogWarning($"Medical exam result {id} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("med_exam_result", "EDIT_NOTFOUND", id.ToString(), null, null,
                        $"Medical exam result not found for edit - ID: {id}, Plant: {userPlantId}");
                    return NotFound("Medical examination result not found or access denied.");
                }

                // Check if user can edit (must be creator and not approved)
                if (!model.CanEdit)
                {
                    _logger.LogWarning($"User {currentUsername} cannot edit result {id}");
                    await _auditService.LogAsync("med_exam_result", "EDIT_DENIED", id.ToString(), null, null,
                        $"Edit access denied - User: {currentUsername}, ID: {id}, Plant: {userPlantId}");
                    return Forbid();
                }

                // Check if approved
                if (model.ApprovalStatus == "Approved")
                {
                    _logger.LogWarning($"Cannot edit approved result {id}");
                    await _auditService.LogAsync("med_exam_result", "EDIT_APPROVED", id.ToString(), null, null,
                        $"Cannot edit approved result - ID: {id}, Plant: {userPlantId}");
                    TempData["ErrorMessage"] = "Cannot edit an approved medical examination result.";
                    return RedirectToAction(nameof(Index));
                }

                await _auditService.LogViewAsync("med_exam_result", id.ToString(),
                    $"Medical exam result edit form accessed - Employee: {model.EmployeeName}, Plant: {userPlantId}");

                model.IsNewEntry = false;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for medical exam result {id}");
                await _auditService.LogAsync("med_exam_result", "EDIT_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");
                return BadRequest($"Error loading edit form: {ex.Message}");
            }
        }

        // POST: Update - Update medical examination result
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(MedExaminationResultViewModel model)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUsername = GetCurrentUsername();

                _logger.LogInformation($"Updating medical exam result for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "UPDATE_ATTEMPT", model.ResultId?.ToString() ?? "null", null, null,
                    $"Medical exam result update attempted - EmpNo: {model.EmpNo}, ResultID: {model.ResultId}, Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    _logger.LogWarning("User has no plant assigned");
                    await _auditService.LogAsync("med_exam_result", "UPDATE_NOPLANT", model.ResultId?.ToString() ?? "null", null, null,
                        "Update failed - User has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                // Check if user can edit
                var canEdit = await _medExamResultRepository.CanUserEditResultAsync(model.ResultId ?? 0, currentUsername, userPlantId);
                if (!canEdit)
                {
                    _logger.LogWarning($"User {currentUsername} cannot edit result {model.ResultId}");
                    await _auditService.LogAsync("med_exam_result", "UPDATE_DENIED", model.ResultId?.ToString() ?? "null", null, null,
                        $"Update denied - User: {currentUsername}, ResultID: {model.ResultId}, Plant: {userPlantId}");
                    return Json(new { success = false, message = "You do not have permission to edit this result." });
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed");
                    await _auditService.LogAsync("med_exam_result", "UPDATE_INVALID", model.ResultId?.ToString() ?? "null", null, null,
                        $"Update validation failed - EmpNo: {model.EmpNo}");

                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                    }

                    var reloadData = await _medExamResultRepository.GetResultForEditAsync(model.ResultId ?? 0, userPlantId, currentUsername);
                    if (reloadData != null)
                    {
                        // Retain edited values
                        reloadData.CatId = model.CatId;
                        reloadData.LastCheckupDate = model.LastCheckupDate;
                        reloadData.TestDate = model.TestDate;
                        reloadData.LocationId = model.LocationId;
                        reloadData.Result = model.Result;
                        reloadData.Remarks = model.Remarks;

                        Response.StatusCode = 400;
                        return PartialView("_MedExamResultFormPartial", reloadData);
                    }
                }

                model.PlantId = (short)userPlantId.Value;
                await _medExamResultRepository.UpdateFormDataAsync(model, userPlantId, currentUsername);

                _logger.LogInformation($"Successfully updated medical exam result for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                await _auditService.LogUpdateAsync("med_exam_result", model.ResultId?.ToString() ?? "null",
                    null, new
                    {
                        EmpNo = model.EmpNo,
                        CategoryId = model.CatId,
                        TestDate = model.TestDate,
                        Result = model.Result,
                        PlantId = userPlantId,
                        ModifiedBy = currentUsername
                    },
                    $"Medical exam result updated - EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                return Json(new { success = true, message = "Medical examination result updated successfully." });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, $"Unauthorized update attempt for result {model.ResultId}");
                await _auditService.LogAsync("med_exam_result", "UPDATE_UNAUTHORIZED", model.ResultId?.ToString() ?? "null", null, null,
                    $"Unauthorized update attempt: {ex.Message}");
                return Json(new { success = false, message = "You do not have permission to update this result." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating medical exam result for EmpNo: {model.EmpNo}");
                await _auditService.LogAsync("med_exam_result", "UPDATE_ERROR", model.ResultId?.ToString() ?? "null", null, null,
                    $"Update error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        // POST: Delete - Delete medical examination result
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int resultId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUsername = GetCurrentUsername();

                _logger.LogInformation($"Deleting medical exam result {resultId} for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "DELETE_ATTEMPT", resultId.ToString(), null, null,
                    $"Medical exam result deletion attempted - ID: {resultId}, Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_exam_result", "DELETE_NOPLANT", resultId.ToString(), null, null,
                        "Delete failed - User has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant." });
                }

                // Check if user can delete
                var canDelete = await _medExamResultRepository.CanUserDeleteResultAsync(resultId, currentUsername, userPlantId);
                if (!canDelete)
                {
                    _logger.LogWarning($"User {currentUsername} cannot delete result {resultId}");
                    await _auditService.LogAsync("med_exam_result", "DELETE_DENIED", resultId.ToString(), null, null,
                        $"Delete denied - User: {currentUsername}, ID: {resultId}, Plant: {userPlantId}");
                    return Json(new { success = false, message = "You do not have permission to delete this result." });
                }

                var deleted = await _medExamResultRepository.DeleteExamResultAsync(resultId, userPlantId);

                if (deleted)
                {
                    _logger.LogInformation($"Successfully deleted medical exam result {resultId}");
                    await _auditService.LogDeleteAsync("med_exam_result", resultId.ToString(),
                        new { ResultId = resultId, DeletedBy = currentUsername, PlantId = userPlantId },
                        $"Medical exam result deleted - ID: {resultId}, User: {currentUsername}, Plant: {userPlantId}");
                    return Json(new { success = true, message = "Medical examination result deleted successfully." });
                }
                else
                {
                    _logger.LogWarning($"Failed to delete medical exam result {resultId}");
                    await _auditService.LogAsync("med_exam_result", "DELETE_FAILED", resultId.ToString(), null, null,
                        $"Delete failed - ID: {resultId}, Plant: {userPlantId}");
                    return Json(new { success = false, message = "Failed to delete medical examination result." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting medical exam result {resultId}");
                await _auditService.LogAsync("med_exam_result", "DELETE_ERROR", resultId.ToString(), null, null,
                    $"Delete error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMedExamResultForm(int empNo, int? resultId = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Loading medical exam result form for employee {empNo}, result ID: {resultId}, Plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "LOAD_FORM", empNo.ToString(), null, null,
                    $"Loading form for employee - EmpNo: {empNo}, ResultID: {resultId}, Plant: {userPlantId}");

                var model = await _medExamResultRepository.LoadFormData(empNo, resultId, userPlantId);

                if (model == null)
                {
                    _logger.LogWarning($"Employee {empNo} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("med_exam_result", "FORM_NOTFOUND", empNo.ToString(), null, null,
                        $"Employee not found or access denied - EmpNo: {empNo}, Plant: {userPlantId}");
                    return NotFound("Employee not found or access denied.");
                }

                // If no resultId was provided, this is a new entry
                if (!resultId.HasValue)
                {
                    model.TestDate = DateTime.Now.Date;
                    model.IsNewEntry = true;
                }

                _logger.LogInformation($"Successfully loaded medical exam result form for employee {empNo}, Plant: {userPlantId}");
                await _auditService.LogAsync("med_exam_result", "FORM_SUCCESS", empNo.ToString(), null, null,
                    $"Form loaded successfully - Employee: {model.EmployeeName}, Plant: {userPlantId}");

                return PartialView("_MedExamResultFormPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading medical exam result form for empNo: {empNo}");
                await _auditService.LogAsync("med_exam_result", "FORM_ERROR", empNo.ToString(), null, null,
                    $"Form load error: {ex.Message}");
                return BadRequest($"Error loading medical exam result form: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMedExamResult(MedExaminationResultViewModel model)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUsername = GetCurrentUsername();

                _logger.LogInformation($"SaveMedExamResult called for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "SAVE_ATTEMPT", model.EmpNo.ToString(), null, null,
                    $"Medical exam result save attempted - EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    _logger.LogWarning("User has no plant assigned");
                    await _auditService.LogAsync("med_exam_result", "SAVE_NOPLANT", model.EmpNo.ToString(), null, null,
                        "Save failed - User has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed");
                    await _auditService.LogAsync("med_exam_result", "SAVE_INVALID", model.EmpNo.ToString(), null, null,
                        $"Save validation failed - EmpNo: {model.EmpNo}");

                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                    }

                    var reloadData = await _medExamResultRepository.LoadFormData(model.EmpNo, null, userPlantId);
                    if (reloadData != null)
                    {
                        // Retain edited values
                        reloadData.CatId = model.CatId;
                        reloadData.LastCheckupDate = model.LastCheckupDate;
                        reloadData.TestDate = model.TestDate;
                        reloadData.LocationId = model.LocationId;
                        reloadData.Result = model.Result;
                        reloadData.Remarks = model.Remarks;

                        Response.StatusCode = 400;
                        return PartialView("_MedExamResultFormPartial", reloadData);
                    }
                }

                // Set the plant ID and creator for the medical exam result
                model.PlantId = (short)userPlantId.Value;
                model.CreatedBy = currentUsername;

                await _medExamResultRepository.SaveFormDataAsync(model, userPlantId, currentUsername);
                _logger.LogInformation($"Successfully saved medical exam result for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                await _auditService.LogCreateAsync("med_exam_result", model.EmpNo.ToString(),
                    new
                    {
                        EmpNo = model.EmpNo,
                        CategoryId = model.CatId,
                        TestDate = model.TestDate,
                        Result = model.Result,
                        LocationId = model.LocationId,
                        PlantId = userPlantId,
                        CreatedBy = currentUsername
                    },
                    $"Medical exam result saved - EmpNo: {model.EmpNo}, Category: {model.CatId}, Plant: {userPlantId}");

                return Json(new { success = true, message = "Medical examination result saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving medical exam result for EmpNo: {model.EmpNo}");
                await _auditService.LogAsync("med_exam_result", "SAVE_ERROR", model.EmpNo.ToString(), null, null,
                    $"Save error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployeeNos(string term)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<string>());
                }

                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Searching employee IDs with term: {term}, Plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "SEARCH_EMPNO", "bulk", null, null,
                    $"Employee ID search - Term: {term}, Plant: {userPlantId}");

                var employeeIds = await _medExamResultRepository.GetMatchingEmployeeIdsAsync(term, userPlantId);

                _logger.LogInformation($"Found {employeeIds.Count} matching employee IDs for plant {userPlantId}");
                await _auditService.LogAsync("med_exam_result", "SEARCH_SUCCESS", "bulk", null, null,
                    $"Employee search completed - Found: {employeeIds.Count} matches, Plant: {userPlantId}");

                return Json(employeeIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching employee IDs with term: {term}");
                await _auditService.LogAsync("med_exam_result", "SEARCH_ERROR", "bulk", null, null,
                    $"Employee search error: {ex.Message}");
                return Json(new List<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUserPlant()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_exam_result", "GET_PLANT", "system", null, null,
                    $"Get current user plant requested - Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_exam_result", "PLANT_NOTFOUND", "system", null, null,
                        "No plant assigned to user");
                    return Json(new { success = false, message = "No plant assigned" });
                }

                var plant = await _db.org_plants.FindAsync((short)userPlantId.Value);

                await _auditService.LogAsync("med_exam_result", "PLANT_SUCCESS", "system", null, null,
                    $"Plant info retrieved - PlantID: {userPlantId}, PlantName: {plant?.plant_name}");

                return Json(new
                {
                    success = true,
                    plantId = userPlantId.Value,
                    plantName = plant?.plant_name ?? "Unknown Plant"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user plant");
                await _auditService.LogAsync("med_exam_result", "PLANT_ERROR", "system", null, null,
                    $"Get plant error: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving plant information" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeUidFromEmpId(string empId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(empId))
                {
                    return Json(new { success = false, message = "Employee ID is required." });
                }

                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Converting Employee ID {empId} to UID for plant {userPlantId}");

                await _auditService.LogAsync("med_exam_result", "CONVERT_EMPID", empId, null, null,
                    $"Converting EmpID to UID - EmpID: {empId}, Plant: {userPlantId}");

                var employeeQuery = _db.HrEmployees.Where(e => e.emp_id == empId);

                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    employeeQuery = employeeQuery.Where(e => e.plant_id == plantIdAsShort);
                }

                var employee = await employeeQuery.FirstOrDefaultAsync();

                if (employee == null)
                {
                    _logger.LogWarning($"Employee with ID {empId} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("med_exam_result", "CONVERT_NOTFOUND", empId, null, null,
                        $"Employee not found - EmpID: {empId}, Plant: {userPlantId}");
                    return Json(new { success = false, message = "Employee not found or access denied." });
                }

                _logger.LogInformation($"Successfully converted Employee ID {empId} to UID {employee.emp_uid}");
                await _auditService.LogAsync("med_exam_result", "CONVERT_SUCCESS", empId, null, null,
                    $"Conversion successful - EmpID: {empId}, UID: {employee.emp_uid}, Name: {employee.emp_name}, Plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    empUid = employee.emp_uid,
                    empId = employee.emp_id,
                    empName = employee.emp_name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error converting Employee ID {empId} to UID");
                await _auditService.LogAsync("med_exam_result", "CONVERT_ERROR", empId ?? "null", null, null,
                    $"Conversion error: {ex.Message}");
                return Json(new { success = false, message = "Error finding employee." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetExamCategories()
        {
            try
            {
                await _auditService.LogAsync("med_exam_result", "GET_CATEGORIES", "system", null, null,
                    "Exam categories requested");

                var categories = await _medExamResultRepository.GetExamCategoriesAsync();

                await _auditService.LogAsync("med_exam_result", "CATEGORIES_OK", "system", null, null,
                    $"Exam categories loaded - Count: {categories.Count()}");

                return Json(categories.Select(c => new
                {
                    catId = c.CatId,
                    catName = c.CatName,
                    yearsFreq = c.YearsFreq,
                    annuallyRule = c.AnnuallyRule
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exam categories");
                await _auditService.LogAsync("med_exam_result", "CATEGORIES_ERROR", "system", null, null,
                    $"Get categories error: {ex.Message}");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTestLocations()
        {
            try
            {
                await _auditService.LogAsync("med_exam_result", "GET_LOCATIONS", "system", null, null,
                    "Test locations requested");

                var locations = await _medExamResultRepository.GetTestLocationsAsync();

                await _auditService.LogAsync("med_exam_result", "LOCATIONS_OK", "system", null, null,
                    $"Test locations loaded - Count: {locations.Count()}");

                return Json(locations.Select(l => new
                {
                    locationId = l.LocationId,
                    locationName = l.LocationName
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting test locations");
                await _auditService.LogAsync("med_exam_result", "LOCATIONS_ERROR", "system", null, null,
                    $"Get locations error: {ex.Message}");
                return Json(new List<object>());
            }
        }
    }
}