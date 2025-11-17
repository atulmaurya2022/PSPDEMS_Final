using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessEmployeeImmunization")]
    public class EmployeeImmunizationController : Controller
    {
        private readonly IImmunizationRepository _immunizationRepository;
        private readonly ILogger<EmployeeImmunizationController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public EmployeeImmunizationController(
            IImmunizationRepository immunizationRepository,
            ILogger<EmployeeImmunizationController> logger,
            ApplicationDbContext db,
            IAuditService auditService)
        {
            _immunizationRepository = immunizationRepository;
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

                return await _immunizationRepository.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user plant ID");
                await _auditService.LogAsync("employee_immunization", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Immunization Index accessed for plant: {userPlantId}");

                await _auditService.LogAsync("employee_immunization", "INDEX_VIEW", "main", null, null,
                    $"Employee immunization list accessed - Plant: {userPlantId}");

                var model = new ImmunizationViewModel
                {
                    ImmunizationTypes = await _immunizationRepository.GetImmunizationTypesAsync()
                };

                await _auditService.LogAsync("employee_immunization", "INDEX_SUCCESS", "main", null, null,
                    $"Immunization types loaded - Count: {model.ImmunizationTypes?.Count() ?? 0}, Plant: {userPlantId}");

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Immunization Index page");
                await _auditService.LogAsync("employee_immunization", "INDEX_FAILED", "main", null, null,
                    $"Failed to load employee immunization index: {ex.Message}");
                return View(new ImmunizationViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllImmunizationRecords()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name + " - " + User.GetFullName();

                await _auditService.LogAsync("employee_immunization", "GET_ALL_RECORDS", "bulk", null, null,
                    $"All immunization records requested - Plant: {userPlantId}");

                var query = _db.MedImmunizationRecords
                    .Include(r => r.RefImmunizationType)
                    .Include(r => r.HrEmployee)
                    .AsQueryable();

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(r => r.plant_id == userPlantId.Value);
                }

                var records = await query
                    .OrderByDescending(r => r.created_date)
                    .Select(r => new
                    {
                        recordId = r.immun_record_uid,
                        empId = r.HrEmployee.emp_id,
                        empName = r.HrEmployee.emp_name,
                        immunizationType = r.RefImmunizationType.immun_type_name,
                        patientName = r.patient_name,
                        relationship = r.relationship,
                        createdBy = r.created_by,
                        createdDate = r.created_date.ToString("dd/MM/yyyy HH:mm"),
                        isCreator = r.created_by == userName
                    })
                    .ToListAsync();

                await _auditService.LogAsync("employee_immunization", "RECORDS_SUCCESS", "bulk", null, null,
                    $"Immunization records loaded - Count: {records.Count}, Plant: {userPlantId}");

                return Json(new { success = true, data = records });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all immunization records");
                await _auditService.LogAsync("employee_immunization", "RECORDS_FAILED", "bulk", null, null,
                    $"Get all records failed: {ex.Message}");
                return Json(new { success = false, message = "Error loading records" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeImmunizationView(int empNo, int? immunizationTypeId = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Loading immunization VIEW for employee {empNo}, immunization type: {immunizationTypeId}, Plant: {userPlantId}");

                await _auditService.LogAsync("employee_immunization", "VIEW_ATTEMPT", empNo.ToString(), null, null,
                    $"Immunization view requested - EmpNo: {empNo}, TypeID: {immunizationTypeId}, Plant: {userPlantId}");

                var model = await _immunizationRepository.LoadFormData(empNo, userPlantId);

                if (model == null)
                {
                    _logger.LogWarning($"Employee {empNo} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("employee_immunization", "VIEW_NOTFOUND", empNo.ToString(), null, null,
                        $"Employee not found or access denied - EmpNo: {empNo}, Plant: {userPlantId}");
                    return NotFound("Employee not found or access denied.");
                }

                // Set the immunization type if provided
                if (immunizationTypeId.HasValue && immunizationTypeId.Value > 0)
                {
                    model.ImmunizationTypeId = immunizationTypeId.Value;

                    // Filter existing records by immunization type
                    model.ExistingRecords = await _immunizationRepository.GetExistingRecordsAsync(
                        empNo, userPlantId, immunizationTypeId.Value);
                }

                _logger.LogInformation($"Successfully loaded immunization VIEW for employee {empNo}, Plant: {userPlantId}");
                await _auditService.LogViewAsync("employee_immunization", empNo.ToString(),
                    $"Immunization view accessed - Employee No: {model.EmpNo}, Records: {model.ExistingRecords?.Count ?? 0}, Plant: {userPlantId}");

                return PartialView("_ImmunizationViewPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading employee immunization VIEW for empNo: {empNo}");
                await _auditService.LogAsync("employee_immunization", "VIEW_ERROR", empNo.ToString(), null, null,
                    $"View error: {ex.Message}");
                return BadRequest($"Error loading employee immunization view: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeImmunizationEdit(int empNo, int? immunizationTypeId = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name + " - " + User.GetFullName();

                _logger.LogInformation($"Loading immunization EDIT for employee {empNo}, immunization type: {immunizationTypeId}, Plant: {userPlantId}");

                await _auditService.LogAsync("employee_immunization", "EDIT_ATTEMPT", empNo.ToString(), null, null,
                    $"Immunization edit requested - EmpNo: {empNo}, TypeID: {immunizationTypeId}, User: {userName}, Plant: {userPlantId}");

                var model = await _immunizationRepository.LoadFormData(empNo, userPlantId);

                if (model == null)
                {
                    _logger.LogWarning($"Employee {empNo} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("employee_immunization", "EDIT_NOTFOUND", empNo.ToString(), null, null,
                        $"Employee not found or access denied - EmpNo: {empNo}, Plant: {userPlantId}");
                    return NotFound("Employee not found or access denied.");
                }

                // Set the immunization type if provided
                if (immunizationTypeId.HasValue && immunizationTypeId.Value > 0)
                {
                    model.ImmunizationTypeId = immunizationTypeId.Value;

                    // Filter existing records by immunization type and only show records created by current user
                    var allRecords = await _immunizationRepository.GetExistingRecordsAsync(
                        empNo, userPlantId, immunizationTypeId.Value);

                    // Filter to only records created by current user (for editing)
                    model.ExistingRecords = allRecords
                        .Where(r => r.created_by == userName)
                        .ToList();
                }

                _logger.LogInformation($"Successfully loaded immunization EDIT for employee {empNo}, Plant: {userPlantId}");
                await _auditService.LogAsync("employee_immunization", "EDIT_SUCCESS", empNo.ToString(), null, null,
                    $"Immunization edit loaded - Employee No: {model.EmpNo}, Editable Records: {model.ExistingRecords?.Count ?? 0}, Plant: {userPlantId}");

                return PartialView("_ImmunizationEditPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading employee immunization EDIT for empNo: {empNo}");
                await _auditService.LogAsync("employee_immunization", "EDIT_ERROR", empNo.ToString(), null, null,
                    $"Edit load error: {ex.Message}");
                return BadRequest($"Error loading employee immunization edit: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetImmunizationTypes()
        {
            try
            {
                await _auditService.LogAsync("employee_immunization", "GET_TYPES", "system", null, null,
                    "Immunization types requested");

                var types = await _immunizationRepository.GetImmunizationTypesAsync();

                await _auditService.LogAsync("employee_immunization", "TYPES_SUCCESS", "system", null, null,
                    $"Immunization types loaded - Count: {types.Count()}");

                return Json(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting immunization types");
                await _auditService.LogAsync("employee_immunization", "TYPES_ERROR", "system", null, null,
                    $"Get types error: {ex.Message}");
                return Json(new List<RefImmunizationType>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeImmunizationForm(int empNo, int? immunizationTypeId = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Loading immunization form for employee {empNo}, immunization type: {immunizationTypeId}, Plant: {userPlantId}");

                await _auditService.LogAsync("employee_immunization", "LOAD_FORM", empNo.ToString(), null, null,
                    $"Immunization form requested - EmpNo: {empNo}, TypeID: {immunizationTypeId}, Plant: {userPlantId}");

                var model = await _immunizationRepository.LoadFormData(empNo, userPlantId);

                if (model == null)
                {
                    _logger.LogWarning($"Employee {empNo} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("employee_immunization", "FORM_NOTFOUND", empNo.ToString(), null, null,
                        $"Employee not found or access denied - EmpNo: {empNo}, Plant: {userPlantId}");
                    return NotFound("Employee not found or access denied.");
                }

                // Set the immunization type if provided
                if (immunizationTypeId.HasValue && immunizationTypeId.Value > 0)
                {
                    model.ImmunizationTypeId = immunizationTypeId.Value;

                    // Filter existing records by immunization type
                    model.ExistingRecords = await _immunizationRepository.GetExistingRecordsAsync(
                        empNo, userPlantId, immunizationTypeId.Value);

                    // Check if there's an existing record for this combination with any patient
                    // This helps determine the workflow state
                    await EnhanceModelWithExistingRecordInfo(model, userPlantId);
                }

                _logger.LogInformation($"Successfully loaded immunization form for employee {empNo}, Plant: {userPlantId}");
                await _auditService.LogAsync("employee_immunization", "FORM_SUCCESS", empNo.ToString(), null, null,
                    $"Immunization form loaded - Employee No: {model.EmpNo}, Plant: {userPlantId}");

                return PartialView("_ImmunizationFormPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading employee immunization form for empNo: {empNo}");
                await _auditService.LogAsync("employee_immunization", "FORM_ERROR", empNo.ToString(), null, null,
                    $"Form load error: {ex.Message}");
                return BadRequest($"Error loading employee immunization form: {ex.Message}");
            }
        }

        private async Task EnhanceModelWithExistingRecordInfo(ImmunizationViewModel model, int? userPlantId)
        {
            // This method can be used to provide additional context about existing records
            // For now, we'll keep it simple but it can be expanded later
            if (model.ImmunizationTypeId.HasValue)
            {
                var existingForType = await _immunizationRepository.GetExistingRecordsAsync(
                    model.EmpNo, userPlantId, model.ImmunizationTypeId.Value);

                // Add any additional logic here if needed
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckExistingRecord(int empNo, int immunizationTypeId, string patientName)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("employee_immunization", "CHECK_EXISTING", empNo.ToString(), null, null,
                    $"Checking existing record - EmpNo: {empNo}, TypeID: {immunizationTypeId}, Patient: {patientName}, Plant: {userPlantId}");

                var existingRecord = await _immunizationRepository.FindIncompleteRecordAsync(
                    empNo, immunizationTypeId, patientName, userPlantId);

                if (existingRecord != null)
                {
                    var nextDoseInfo = _immunizationRepository.GetNextDoseInfo(existingRecord);

                    await _auditService.LogAsync("employee_immunization", "EXISTING_FOUND", empNo.ToString(), null, null,
                        $"Existing record found - RecordID: {existingRecord.immun_record_uid}, IsComplete: {nextDoseInfo.IsComplete}, Plant: {userPlantId}");

                    return Json(new
                    {
                        exists = true,
                        recordId = existingRecord.immun_record_uid,
                        nextDose = nextDoseInfo,
                        isComplete = nextDoseInfo.IsComplete,
                        message = nextDoseInfo.IsComplete
                            ? "All doses for this immunization are complete."
                            : $"Existing record found. {nextDoseInfo.DisplayText}.",
                        dose1Date = existingRecord.dose_1_date?.ToString("yyyy-MM-dd"),
                        dose2Date = existingRecord.dose_2_date?.ToString("yyyy-MM-dd"),
                        dose3Date = existingRecord.dose_3_date?.ToString("yyyy-MM-dd"),
                        dose4Date = existingRecord.dose_4_date?.ToString("yyyy-MM-dd"),
                        dose5Date = existingRecord.dose_5_date?.ToString("yyyy-MM-dd"),
                        boosterDoseDate = existingRecord.booster_dose_date?.ToString("yyyy-MM-dd"),
                        remarks = existingRecord.remarks
                    });
                }

                await _auditService.LogAsync("employee_immunization", "EXISTING_NONE", empNo.ToString(), null, null,
                    $"No existing record found - EmpNo: {empNo}, TypeID: {immunizationTypeId}, Plant: {userPlantId}");

                return Json(new { exists = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existing record");
                await _auditService.LogAsync("employee_immunization", "CHECK_ERROR", empNo.ToString(), null, null,
                    $"Check existing record error: {ex.Message}");
                return Json(new { exists = false, error = "Error checking existing record" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveImmunizationRecord(ImmunizationViewModel model)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name + " - " + User.GetFullName();

                _logger.LogInformation($"SaveImmunizationRecord called for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                await _auditService.LogAsync("employee_immunization", "SAVE_ATTEMPT", model.EmpNo.ToString(), null, null,
                    $"Immunization record save attempted - EmpNo: {model.EmpNo}, TypeID: {model.ImmunizationTypeId}, Patient: {model.PatientName}, Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    _logger.LogWarning("User has no plant assigned");
                    await _auditService.LogAsync("employee_immunization", "SAVE_NOPLANT", model.EmpNo.ToString(), null, null,
                        "Save failed - User has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed");
                    await _auditService.LogAsync("employee_immunization", "SAVE_INVALID", model.EmpNo.ToString(), null, null,
                        $"Save validation failed - EmpNo: {model.EmpNo}");

                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                    }

                    var reloadData = await _immunizationRepository.LoadFormData(model.EmpNo, userPlantId);
                    if (reloadData != null)
                    {
                        // Retain edited values
                        reloadData.ImmunizationTypeId = model.ImmunizationTypeId;
                        reloadData.PatientName = model.PatientName;
                        reloadData.Relationship = model.Relationship;
                        reloadData.Dose1Date = model.Dose1Date;
                        reloadData.Dose2Date = model.Dose2Date;
                        reloadData.Dose3Date = model.Dose3Date;
                        reloadData.Dose4Date = model.Dose4Date;
                        reloadData.Dose5Date = model.Dose5Date;
                        reloadData.BoosterDoseDate = model.BoosterDoseDate;
                        reloadData.Remarks = model.Remarks;
                        reloadData.RecordId = model.RecordId;

                        Response.StatusCode = 400;
                        return PartialView("_ImmunizationFormPartial", reloadData);
                    }
                }

                // Set the plant ID for the immunization record
                model.PlantId = (short)userPlantId.Value;

                await _immunizationRepository.SaveImmunizationRecordAsync(model, userPlantId, userName);
                _logger.LogInformation($"Successfully saved immunization record for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                // Log successful save (critical operation)
                await _auditService.LogCreateAsync("employee_immunization", model.EmpNo.ToString(),
                    new
                    {
                        EmpNo = model.EmpNo,
                        ImmunizationTypeId = model.ImmunizationTypeId,
                        PatientName = model.PatientName,
                        Relationship = model.Relationship,
                        RecordId = model.RecordId,
                        PlantId = userPlantId,
                        CreatedBy = userName
                    },
                    $"Immunization record saved - EmpNo: {model.EmpNo}, Patient: {model.PatientName}, Type: {model.ImmunizationTypeId}, Plant: {userPlantId}");

                return Json(new { success = true, message = "Immunization record saved successfully." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"Business logic validation failed for EmpNo: {model.EmpNo}");
                await _auditService.LogAsync("employee_immunization", "SAVE_BUSINESS_ERR", model.EmpNo.ToString(), null, null,
                    $"Business logic error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, $"Argument validation failed for EmpNo: {model.EmpNo}");
                await _auditService.LogAsync("employee_immunization", "SAVE_ARG_ERR", model.EmpNo.ToString(), null, null,
                    $"Argument error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving employee immunization record for EmpNo: {model.EmpNo}");
                await _auditService.LogAsync("employee_immunization", "SAVE_ERROR", model.EmpNo.ToString(), null, null,
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

                await _auditService.LogAsync("employee_immunization", "SEARCH_EMPNO", "bulk", null, null,
                    $"Employee ID search - Term: {term}, Plant: {userPlantId}");

                var employeeIds = await _immunizationRepository.GetMatchingEmployeeIdsAsync(term, userPlantId);

                _logger.LogInformation($"Found {employeeIds.Count} matching employee IDs for plant {userPlantId}");
                await _auditService.LogAsync("employee_immunization", "SEARCH_SUCCESS", "bulk", null, null,
                    $"Employee search completed - Found: {employeeIds.Count} matches, Plant: {userPlantId}");

                return Json(employeeIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching employee IDs with term: {term}");
                await _auditService.LogAsync("employee_immunization", "SEARCH_ERROR", "bulk", null, null,
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

                await _auditService.LogAsync("employee_immunization", "GET_PLANT", "system", null, null,
                    $"Get current user plant requested - Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("employee_immunization", "PLANT_NOTFOUND", "system", null, null,
                        "No plant assigned to user");
                    return Json(new { success = false, message = "No plant assigned" });
                }

                // Cast to short since org_plants primary key is of type short
                var plant = await _db.org_plants.FindAsync((short)userPlantId.Value);

                await _auditService.LogAsync("employee_immunization", "PLANT_SUCCESS", "system", null, null,
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
                await _auditService.LogAsync("employee_immunization", "PLANT_ERROR", "system", null, null,
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

                await _auditService.LogAsync("employee_immunization", "CONVERT_EMPID", empId, null, null,
                    $"Converting EmpID to UID - EmpID: {empId}, Plant: {userPlantId}");

                var employeeQuery = _db.HrEmployees.Where(e => e.emp_id == empId);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    employeeQuery = employeeQuery.Where(e => e.plant_id == userPlantId.Value);
                }

                var employee = await employeeQuery.FirstOrDefaultAsync();

                if (employee == null)
                {
                    _logger.LogWarning($"Employee with ID {empId} not found or access denied for plant {userPlantId}");
                    await _auditService.LogAsync("employee_immunization", "CONVERT_NOTFOUND", empId, null, null,
                        $"Employee not found - EmpID: {empId}, Plant: {userPlantId}");
                    return Json(new { success = false, message = "Employee not found or access denied." });
                }

                _logger.LogInformation($"Successfully converted Employee ID {empId} to UID {employee.emp_uid}");
                await _auditService.LogAsync("employee_immunization", "CONVERT_SUCCESS", empId, null, null,
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
                await _auditService.LogAsync("employee_immunization", "CONVERT_ERROR", empId ?? "null", null, null,
                    $"Conversion error: {ex.Message}");
                return Json(new { success = false, message = "Error finding employee." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImmunizationRecord(int recordId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Deleting immunization record {recordId}, Plant: {userPlantId}");

                await _auditService.LogAsync("employee_immunization", "DELETE_ATTEMPT", recordId.ToString(), null, null,
                    $"Immunization record deletion attempted - RecordID: {recordId}, Plant: {userPlantId}");

                var success = await _immunizationRepository.DeleteImmunizationRecordAsync(recordId, userPlantId);

                if (success)
                {
                    _logger.LogInformation($"Successfully deleted immunization record {recordId}");

                    // Log successful deletion (critical operation)
                    await _auditService.LogDeleteAsync("employee_immunization", recordId.ToString(),
                        new { RecordId = recordId, PlantId = userPlantId },
                        $"Immunization record deleted - RecordID: {recordId}, Plant: {userPlantId}");

                    return Json(new { success = true, message = "Record deleted successfully." });
                }
                else
                {
                    _logger.LogWarning($"Failed to delete immunization record {recordId} - not found or access denied");
                    await _auditService.LogAsync("employee_immunization", "DELETE_NOTFOUND", recordId.ToString(), null, null,
                        $"Record not found or access denied - RecordID: {recordId}, Plant: {userPlantId}");
                    return Json(new { success = false, message = "Record not found or access denied." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting immunization record {recordId}");
                await _auditService.LogAsync("employee_immunization", "DELETE_ERROR", recordId.ToString(), null, null,
                    $"Delete error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the record." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetImmunizationRecord(int recordId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("employee_immunization", "GET_RECORD", recordId.ToString(), null, null,
                    $"Immunization record requested - RecordID: {recordId}, Plant: {userPlantId}");

                var record = await _immunizationRepository.GetImmunizationRecordAsync(recordId, userPlantId);

                if (record == null)
                {
                    await _auditService.LogAsync("employee_immunization", "RECORD_NOTFOUND", recordId.ToString(), null, null,
                        $"Record not found or access denied - RecordID: {recordId}, Plant: {userPlantId}");
                    return NotFound("Record not found or access denied.");
                }

                // Get next dose info
                var nextDoseInfo = _immunizationRepository.GetNextDoseInfo(record);

                var model = new ImmunizationViewModel
                {
                    RecordId = record.immun_record_uid,
                    EmpNo = record.emp_uid,
                    ImmunizationTypeId = record.immun_type_uid,
                    PatientName = record.patient_name,
                    Relationship = record.relationship,
                    Dose1Date = record.dose_1_date?.ToDateTime(TimeOnly.MinValue),
                    Dose2Date = record.dose_2_date?.ToDateTime(TimeOnly.MinValue),
                    Dose3Date = record.dose_3_date?.ToDateTime(TimeOnly.MinValue),
                    Dose4Date = record.dose_4_date?.ToDateTime(TimeOnly.MinValue),
                    Dose5Date = record.dose_5_date?.ToDateTime(TimeOnly.MinValue),
                    BoosterDoseDate = record.booster_dose_date?.ToDateTime(TimeOnly.MinValue),
                    Remarks = record.remarks,
                    IsNewEntry = false,
                    NextDoseInfo = nextDoseInfo,
                    IsUpdatingExistingRecord = true
                };

                await _auditService.LogViewAsync("employee_immunization", recordId.ToString(),
                    $"Immunization record retrieved - RecordID: {recordId}, Patient: {record.patient_name}, Plant: {userPlantId}");

                return Json(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting immunization record {recordId}");
                await _auditService.LogAsync("employee_immunization", "RECORD_ERROR", recordId.ToString(), null, null,
                    $"Get record error: {ex.Message}");
                return BadRequest("Error retrieving record.");
            }
        }
    }
}