using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessDoctorDiagnosis")]
    public class DoctorDiagnosisController : Controller
    {
        private readonly IDoctorDiagnosisRepository _doctorDiagnosisRepository;
        private readonly IHealthProfileRepository _healthProfileRepository;
        private readonly IMedicalDataMaskingService _maskingService;
        private readonly IAuditService _auditService;
        private readonly ILogger<DoctorDiagnosisController> _logger;

        public DoctorDiagnosisController(
            IDoctorDiagnosisRepository doctorDiagnosisRepository,
            IHealthProfileRepository healthProfileRepository,
            IMedicalDataMaskingService maskingService,
            IAuditService auditService,
            ILogger<DoctorDiagnosisController> logger)
        {
            _doctorDiagnosisRepository = doctorDiagnosisRepository;
            _healthProfileRepository = healthProfileRepository;
            _maskingService = maskingService;
            _auditService = auditService;
            _logger = logger;
        }



        public async Task<IActionResult> Index()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();

                // NEW: Get current user identifier
                var currentUser = User.FindFirst("user_id")?.Value ??
                                 User.Identity?.Name + " - " + User.GetFullName() ??
                                 "unknown";

                ViewBag.UserRole = userRole;
                ViewBag.IsDoctor = userRole?.ToLower() == "doctor";
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);
                ViewBag.CurrentUser = currentUser; // NEW: Pass current user to view

                var diagnoses = await _doctorDiagnosisRepository.GetAllEmployeeDiagnosesAsync(userPlantId);

                await _auditService.LogAsync("doctor_diagnosis", "INDEX_VIEW", "main", null, null,
                    $"Doctor diagnosis list loaded - Count: {diagnoses.Count()}, Role: {userRole}, Plant: {userPlantId}");

                return View(diagnoses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor diagnosis list");
                await _auditService.LogAsync("doctor_diagnosis", "INDEX_FAILED", "main", null, null,
                    $"Failed to load doctor diagnosis index: {ex.Message}");
                return View(new List<EmployeeDiagnosisListViewModel>());
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();

                ViewBag.UserRole = userRole;
                ViewBag.UserPlantId = userPlantId;
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                var model = new DoctorDiagnosisViewModel();

                await _auditService.LogAsync("doctor_diagnosis", "CREATE_FORM", "main", null, null,
                    $"Doctor diagnosis create form accessed by user role: {userRole}, Plant: {userPlantId}");

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor diagnosis create form");
                await _auditService.LogAsync("doctor_diagnosis", "CREATE_FAILED", "main", null, null,
                    $"Failed to load doctor diagnosis create form: {ex.Message}");
                TempData["Error"] = "Error loading create form.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployee(string empId, DateTime? examDate = null, string? visitType = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log search attempt with plant info
                await _auditService.LogAsync("doctor_diagnosis", "SEARCH_ATTEMPT", empId ?? "null", null, null,
                    $"Employee search attempted - EmpId: {empId}, ExamDate: {examDate}, VisitType: {visitType}, Plant: {userPlantId}");

                if (string.IsNullOrWhiteSpace(empId))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SEARCH_INVALID", "null", null, null,
                        "Employee search failed - Employee ID is required");
                    return Json(new { success = false, message = "Employee ID is required." });
                }

                // Find employee by emp_id with plant filtering
                var employee = await _doctorDiagnosisRepository.GetEmployeeByEmpIdAsync(empId, userPlantId);

                if (employee == null)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SEARCH_NOTFOUND", empId, null, null,
                        $"Employee not found for ID: {empId} in plant: {userPlantId}");
                    return Json(new { success = false, message = "Employee not found in your plant." });
                }

                // Set exam date to today if not provided
                var searchDate = examDate ?? DateTime.Now.Date;
                var selectedVisitType = visitType ?? "Regular Visitor";

                // Load health profile data with plant filtering
                var healthProfile = await _healthProfileRepository.LoadFormData(employee.emp_uid, searchDate);
                var medConditions = await _doctorDiagnosisRepository.GetMedicalConditionsAsync();

                var model = new DoctorDiagnosisViewModel
                {
                    VisitType = selectedVisitType,
                    EmpId = empId,
                    ExamDate = searchDate,
                    Employee = employee,
                    MedConditions = medConditions,
                    SelectedConditionIds = healthProfile?.SelectedConditionIds ?? new List<int>()
                };

                // Log successful search with plant info
                await _auditService.LogAsync("doctor_diagnosis", "SEARCH_SUCCESS", empId, null, null,
                    $"Employee found and data loaded - Name: {employee.emp_name}, Department: {employee.org_department}, Plant: {employee.org_plant?.plant_name}");

                return Json(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for employee with ID: {empId}");
                await _auditService.LogAsync("doctor_diagnosis", "SEARCH_FAILED", empId ?? "null", null, null,
                    $"Employee search failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while searching for the employee." });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetEmployeeDetails(string empId, DateTime? examDate = null, string? visitType = null, string? patientStatus = null, string? dependentName = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                _logger.LogInformation($"🔍 GetEmployeeDetails called - empId: '{empId}', examDate: {examDate}, visitType: '{visitType}', patientStatus: '{patientStatus}', dependentName: '{dependentName}', Plant: {userPlantId}");

                // Log access attempt to sensitive medical data with plant info
                await _auditService.LogAsync("doctor_diagnosis", "GET_DETAILS", empId ?? "null", null, null,
                    $"Employee details access attempted - ExamDate: {examDate}, VisitType: {visitType}, PatientStatus: {patientStatus}, Dependent: {dependentName}, Plant: {userPlantId}");

                if (string.IsNullOrWhiteSpace(empId))
                {
                    _logger.LogWarning("❌ Employee ID is null or empty");
                    await _auditService.LogAsync("doctor_diagnosis", "DETAILS_INVALID", "null", null, null,
                        "Employee details access failed - Employee ID is required");
                    return BadRequest("Employee ID is required.");
                }

                _logger.LogInformation($"🔍 Searching for employee with ID: '{empId}' in plant: {userPlantId}");
                var employee = await _doctorDiagnosisRepository.GetEmployeeByEmpIdAsync(empId, userPlantId);

                if (employee == null)
                {
                    _logger.LogWarning($"Employee not found for ID: '{empId}' in plant: {userPlantId}");
                    await _auditService.LogAsync("doctor_diagnosis", "DETAILS_NOTFOUND", empId, null, null,
                        $"Employee not found for details access: {empId} in plant: {userPlantId}");
                    return NotFound($"Employee with ID '{empId}' not found in your plant.");
                }

                _logger.LogInformation($"✅ Employee found: {employee.emp_name} in plant: {employee.org_plant?.plant_name}");

                var searchDate = examDate ?? DateTime.Now.Date;
                var selectedVisitType = visitType ?? "Regular Visitor";
                var selectedPatientStatus = patientStatus ?? "On Duty";
                var selectedDependentName = dependentName ?? "Self";

                _logger.LogInformation($"Loading health profile for date: {searchDate}, visit type: {selectedVisitType}, patient status: {selectedPatientStatus}, dependent: {selectedDependentName}");

                var healthProfile = await _healthProfileRepository.LoadFormData(employee.emp_uid, searchDate);
                var medConditions = await _doctorDiagnosisRepository.GetMedicalConditionsAsync();

                _logger.LogInformation($"✅ Health profile loaded, conditions count: {medConditions.Count}");

                // NEW: Get dependent details if dependent is selected
                HrEmployeeDependent? dependentDetails = null;
                if (!string.IsNullOrEmpty(selectedDependentName) && selectedDependentName != "Self")
                {
                    dependentDetails = await GetEmployeeDependentDetailsAsync(employee.emp_uid, selectedDependentName);
                    _logger.LogInformation($"🔍 Dependent details loaded: {dependentDetails?.dep_name ?? "Not found"}");
                }

                var model = new DoctorDiagnosisViewModel
                {
                    VisitType = selectedVisitType,
                    EmpId = empId,
                    ExamDate = searchDate,
                    Employee = employee,
                    MedConditions = medConditions,
                    SelectedConditionIds = healthProfile?.SelectedConditionIds ?? new List<int>(),
                    PatientStatus = selectedPatientStatus,
                    DependentName = selectedDependentName,

                    // NEW: Add dependent details
                    DependentRelation = dependentDetails?.relation,
                    DependentAge = dependentDetails?.Age,
                    DependentGender = dependentDetails?.gender switch
                    {
                        "M" => "Male",
                        "F" => "Female",
                        "O" => "Other",
                        _ => dependentDetails?.gender
                    },
                    DependentDOB = dependentDetails?.dep_dob?.ToDateTime(TimeOnly.MinValue),
                    DependentIsActive = dependentDetails?.is_active
                };

                // Get user role and apply masking if needed
                var userRole = await GetUserRoleAsync();
                ViewBag.UserRole = userRole;
                ViewBag.UserPlantId = userPlantId;
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                // Mask sensitive data if user doesn't have appropriate role
                _maskingService.MaskObject(model, userRole);

                // Log successful access to sensitive medical data with plant info
                var patientInfo = selectedDependentName != "Self" ? $"Dependent: {selectedDependentName}" : "Employee (Self)";
                await _auditService.LogViewAsync("doctor_diagnosis", empId,
                    $"Employee medical details accessed - Employee: {employee.emp_name}, Patient: {patientInfo}, Role: {userRole}, Plant: {employee.org_plant?.plant_name}, PatientStatus: {selectedPatientStatus}, Masked: {_maskingService.ShouldMaskData(userRole)}");

                return PartialView("_EmployeeDetailsPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error in GetEmployeeDetails for empId: '{empId}' - {ex.Message}");
                await _auditService.LogAsync("doctor_diagnosis", "DETAILS_FAILED", empId ?? "null", null, null,
                    $"Employee details access failed: {ex.Message}");
                return BadRequest($"Error loading employee details: {ex.Message}");
            }
        }

        // NEW: Helper method to get dependent details
        private async Task<HrEmployeeDependent?> GetEmployeeDependentDetailsAsync(int empUid, string dependentName)
        {
            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var dependent = await dbContext.HrEmployeeDependents
                    .FirstOrDefaultAsync(d => d.emp_uid == empUid &&
                                             d.dep_name == dependentName &&
                                             d.is_active);

                return dependent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting dependent details for {dependentName}");
                return null;
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetEmployeeDependents(string empId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log dependent data access attempt with plant info
                await _auditService.LogAsync("doctor_diagnosis", "GET_DEPENDENTS", empId ?? "null", null, null,
                    $"Employee dependents access attempted for: {empId}, Plant: {userPlantId}");

                if (string.IsNullOrWhiteSpace(empId))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "DEP_INVALID", "null", null, null,
                        "Get dependents failed - Employee ID is required");
                    return Json(new { success = false, message = "Employee ID is required." });
                }

                var employee = await _doctorDiagnosisRepository.GetEmployeeByEmpIdAsync(empId, userPlantId);
                if (employee == null)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "DEP_NOTFOUND", empId, null, null,
                        $"Employee not found for dependents access: {empId} in plant: {userPlantId}");
                    return Json(new { success = false, message = "Employee not found in your plant." });
                }

                // Get dependents from database
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var dependents = await dbContext.HrEmployeeDependents
                    .Where(d => d.emp_uid == employee.emp_uid && d.is_active)
                    .Select(d => new
                    {
                        value = d.dep_name,
                        text = $"{d.dep_name} ({d.relation})"
                    })
                    .ToListAsync();

                // Always include "Self" as first option
                var dependentOptions = new List<object>
                {
                    new { value = "Self", text = "Self" }
                };

                dependentOptions.AddRange(dependents);

                // Log successful dependent data access with plant info
                await _auditService.LogAsync("doctor_diagnosis", "DEP_SUCCESS", empId, null, null,
                    $"Dependents loaded for employee: {employee.emp_name}, Count: {dependents.Count}, Plant: {employee.org_plant?.plant_name}");

                return Json(new
                {
                    success = true,
                    dependents = dependentOptions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading dependents for employee {empId}");
                await _auditService.LogAsync("doctor_diagnosis", "DEP_FAILED", empId ?? "null", null, null,
                    $"Get dependents failed: {ex.Message}");
                return Json(new
                {
                    success = false,
                    dependents = new[] { new { value = "Self", text = "Self" } },
                    message = "Error loading dependents"
                });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetPrescriptionData()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                _logger.LogInformation($"🔍 Getting prescription data with FIFO batch selection for plant: {userPlantId}");

                // Log access to prescription/medicine data with plant info
                await _auditService.LogAsync("doctor_diagnosis", "GET_PRESCDATA", "system", null, null,
                    $"Prescription and medicine data access attempted for plant: {userPlantId}");

                // Get plant-wise diseases
                var diseases = await _doctorDiagnosisRepository.GetDiseasesAsync(userPlantId);

                // Get plant-wise medicines with batch information (no grouping - keep each IndentItemId-Batch separate)
                var medicineStocks = await _doctorDiagnosisRepository.GetMedicinesFromCompounderIndentAsync(userPlantId);

                _logger.LogInformation($"✅ Found {diseases.Count} diseases and {medicineStocks.Count} medicine batches for plant: {userPlantId}");

                // Convert medicines to dropdown format - FIXED: Keep each IndentItemId-Batch combination separate
                var medicineDropdownItems = medicineStocks
                    .Where(m => m.AvailableStock > 0) // Only show items with available stock
                    .OrderBy(m => m.ExpiryDate) // Order by medicine name first
                    .ThenBy(m => m.MedItemName) // Then by MedItemName
                    .ThenBy(m => m.BatchNo) // Then by batch number
                                            //.ThenBy(m => m.ExpiryDate ?? DateTime.MaxValue) // Then by expiry date (FIFO)
                    .Select(m => new
                    {
                        indentItemId = m.IndentItemId, // FIXED: Use actual IndentItemId, not grouped
                        medItemId = m.MedItemId,
                        baseName = m.BaseName,
                        //text = $"{m.MedItemId} - {m.BaseName} - {m.MedItemName} | Batch: {m.BatchNo}",
                        text = $"{m.MedItemId} - {(string.IsNullOrEmpty(m.BaseName) || m.BaseName == "Not Defined" ? "" : $"{m.BaseName} - ")}{m.MedItemName} | Batch: {m.BatchNo}",
                        stockInfo = $"Stock: {m.AvailableStock}",
                        expiryInfo = m.ExpiryDateFormatted,
                        daysToExpiry = m.DaysToExpiry,
                        availableStock = m.AvailableStock, // FIXED: Use actual stock for this specific batch
                        batchNo = m.BatchNo,
                        expiryDate = m.ExpiryDate?.ToString("yyyy-MM-dd"),
                        companyName = m.CompanyName,
                        plantId = m.PlantId,
                        medicineName = m.MedItemName,
                        expiryClass = m.DaysToExpiry switch
                        {
                            < 0 => "text-danger",
                            <= 7 => "text-warning",
                            <= 30 => "text-info",
                            _ => "text-success"
                        },
                        expiryLabel = m.DaysToExpiry switch
                        {
                            < 0 => "EXPIRED",
                            <= 7 => $"{m.ExpiryDate?.ToString("yyyy-MM-dd")} - ({m.DaysToExpiry}d)",
                            <= 30 => $"{m.ExpiryDate?.ToString("yyyy-MM-dd")} - ({m.DaysToExpiry}d)",
                            _ => $"Expires: {m.ExpiryDateFormatted}"
                        },
                        isNearExpiry = m.DaysToExpiry <= 30 && m.DaysToExpiry >= 0,
                        isExpired = m.DaysToExpiry < 0
                    })
                    .ToList();

                // Convert diseases to dropdown format with plant info
                var diseaseDropdownItems = diseases.Select(d => new {
                    value = d.DiseaseId,
                    text = $"{d.DiseaseId} - {d.DiseaseName}",
                    description = d.DiseaseDesc ?? ""
                });

                // Log successful data loading with plant info
                await _auditService.LogAsync("doctor_diagnosis", "PRESCDATA_OK", "system", null, null,
                    $"Prescription data loaded - Diseases: {diseases.Count}, Medicine Batches: {medicineStocks.Count}, Plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    diseases = diseaseDropdownItems,
                    medicines = medicineDropdownItems,
                    plantId = userPlantId,
                    summary = new
                    {
                        totalDiseases = diseases.Count,
                        totalMedicineBatches = medicineStocks.Count,
                        expiredBatches = medicineStocks.Count(m => m.DaysToExpiry < 0),
                        nearExpiryBatches = medicineStocks.Count(m => m.DaysToExpiry <= 30 && m.DaysToExpiry >= 0)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription data with FIFO batch selection");
                await _auditService.LogAsync("doctor_diagnosis", "PRESCDATA_FAIL", "system", null, null,
                    $"Prescription data loading failed: {ex.Message}");
                return Json(new
                {
                    success = false,
                    diseases = new List<object>(),
                    medicines = new List<object>(),
                    message = "Error loading medicine data with FIFO batch selection"
                });
            }
        }
        

        [HttpPost]
        public async Task<IActionResult> DeletePrescription(int prescriptionId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();
                var currentUser = User.FindFirst("user_id")?.Value ??
                                 User.Identity?.Name + " - " + User.GetFullName() ??
                                 "unknown";

                await _auditService.LogAsync("doctor_diagnosis", "DELETE_ATTEMPT", prescriptionId.ToString(), null, null,
                    $"Prescription deletion attempted for ID: {prescriptionId}, Plant: {userPlantId}");

                // Check if user has permission to delete prescriptions
                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "DELETE_DENIED", prescriptionId.ToString(), null, null,
                        $"Prescription deletion denied for role: {userRole}");
                    return Json(new { success = false, message = "You don't have permission to delete prescriptions." });
                }

                // NEW: Get prescription details to check creator
                var prescriptionDetails = await _doctorDiagnosisRepository.GetPrescriptionDetailsAsync(prescriptionId, userPlantId);

                if (prescriptionDetails == null)
                {
                    return Json(new { success = false, message = "Prescription not found." });
                }

                // NEW: Check if user is creator or doctor
                var isCreator = prescriptionDetails.CreatedBy == currentUser;
                var isDoctor = userRole?.ToLower() == "doctor";

                if (!isCreator && !isDoctor)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "DELETE_UNAUTHORIZED", prescriptionId.ToString(), null, null,
                        $"User {currentUser} not authorized to delete prescription created by {prescriptionDetails.CreatedBy}");
                    return Json(new { success = false, message = "You can only delete your own prescriptions or if you're a doctor." });
                }

                var deletedBy = currentUser;
                var success = await _doctorDiagnosisRepository.DeletePrescriptionAsync(prescriptionId, userPlantId, deletedBy);

                if (success)
                {
                    await _auditService.LogDeleteAsync("doctor_diagnosis", prescriptionId.ToString(),
                        new { PrescriptionId = prescriptionId, DeletedBy = deletedBy, PlantId = userPlantId },
                        $"Prescription deleted successfully by: {deletedBy} in plant: {userPlantId}");

                    return Json(new { success = true, message = "Prescription deleted successfully." });
                }
                else
                {
                    await _auditService.LogAsync("doctor_diagnosis", "DELETE_FAILED", prescriptionId.ToString(), null, null,
                        $"Prescription deletion failed - database error for ID: {prescriptionId} in plant: {userPlantId}");
                    return Json(new { success = false, message = "Failed to delete prescription." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting prescription {prescriptionId}");
                await _auditService.LogAsync("doctor_diagnosis", "DELETE_ERROR", prescriptionId.ToString(), null, null,
                    $"Prescription deletion failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error deleting prescription: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckMedicineStock(int indentItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log stock check attempt with plant info
                await _auditService.LogAsync("doctor_diagnosis", "CHECK_STOCK", indentItemId.ToString(), null, null,
                    $"Medicine stock check attempted for indent item: {indentItemId}, Plant: {userPlantId}");

                var availableStock = await _doctorDiagnosisRepository.GetAvailableStockAsync(indentItemId, userPlantId);

                // Log stock check result with plant info
                await _auditService.LogAsync("doctor_diagnosis", "STOCK_SUCCESS", indentItemId.ToString(), null, null,
                    $"Medicine stock checked - Available: {availableStock}, Plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    availableStock = availableStock
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking stock for indent item {indentItemId}");
                await _auditService.LogAsync("doctor_diagnosis", "STOCK_FAILED", indentItemId.ToString(), null, null,
                    $"Medicine stock check failed: {ex.Message}");
                return Json(new { success = false, availableStock = 0 });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ValidatePrescriptionStock(List<PrescriptionMedicine> medicines)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log validation attempt with plant info
                await _auditService.LogAsync("doctor_diagnosis", "VALIDATE_STOCK", "bulk", null, null,
                    $"Prescription stock validation attempted for {medicines?.Count ?? 0} medicines, Plant: {userPlantId}");

                var validationResults = new List<StockValidationResult>();

                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await _doctorDiagnosisRepository.GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);

                            var validationResult = new StockValidationResult
                            {
                                IsValid = medicine.Quantity <= availableStock,
                                AvailableStock = availableStock,
                                RequestedQuantity = medicine.Quantity,
                                MedicineName = medicine.MedicineName,
                                BatchNo = medicine.BatchNo ?? "N/A"
                            };

                            if (!validationResult.IsValid)
                            {
                                validationResult.ErrorMessage = $"Insufficient stock for {medicine.MedicineName} (Batch: {medicine.BatchNo}) in your plant. Available: {availableStock}, Requested: {medicine.Quantity}";
                            }

                            validationResults.Add(validationResult);
                        }
                    }
                }

                var allValid = validationResults.All(r => r.IsValid);
                var errorMessages = validationResults.Where(r => !r.IsValid).Select(r => r.ErrorMessage).ToList();

                // Log validation result with plant info
                var validationStatus = allValid ? "SUCCESS" : "FAILED";
                await _auditService.LogAsync("doctor_diagnosis", $"VALID_{validationStatus}", "bulk", null, null,
                    $"Stock validation completed - Valid: {allValid}, Errors: {errorMessages.Count}, Plant: {userPlantId}");

                return Json(new
                {
                    success = allValid,
                    isValid = allValid,
                    errors = errorMessages,
                    validationResults = validationResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating prescription stock");
                await _auditService.LogAsync("doctor_diagnosis", "VALID_ERROR", "bulk", null, null,
                    $"Stock validation failed: {ex.Message}");
                return Json(new { success = false, isValid = false, errors = new[] { "Error validating stock availability" } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SavePrescription(string empId, DateTime examDate,
        List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
        string? bloodPressure, string? pulse, string? temperature, string? remarks = null, string? visitType = null, string? patientStatus = null, string? dependentName = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var selectedVisitType = visitType ?? "Regular Visitor";
                var selectedPatientStatus = patientStatus ?? "On Duty";
                var selectedDependentName = dependentName ?? "Self";

                // Log prescription save attempt (critical operation) with plant and dependent info
                var patientInfo = selectedDependentName != "Self" ? $"Dependent: {selectedDependentName}" : "Employee (Self)";
                await _auditService.LogAsync("doctor_diagnosis", "SAVE_ATTEMPT", empId ?? "null", null, null,
                    $"Prescription save attempted - EmpId: {empId}, Patient: {patientInfo}, ExamDate: {examDate}, VisitType: {selectedVisitType}, PatientStatus: {selectedPatientStatus}, UserRemarks: " + (!string.IsNullOrEmpty(remarks) ? "Provided" : "None") + ", Diseases: {selectedDiseases?.Count ?? 0}, Medicines: {medicines?.Count ?? 0}, Plant: {userPlantId}");

                // Check if user has permission to save prescriptions
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SAVE_DENIED", empId ?? "null", null, null,
                        $"Prescription save denied - insufficient permissions for role: {userRole}");
                    return Json(new { success = false, message = "You don't have permission to save prescriptions." });
                }

                _logger.LogInformation($"💊 Saving prescription for employee {empId} on {examDate} - Visit Type: {selectedVisitType}, Patient: {patientInfo}, Patient Status: {selectedPatientStatus}, Plant: {userPlantId}");

                // Validate input
                if (string.IsNullOrWhiteSpace(empId))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SAVE_INVALID", "null", null, null,
                        "Prescription save failed - Employee ID is required");
                    return Json(new { success = false, message = "Employee ID is required." });
                }

                // Validate employee exists in user's plant
                var employee = await _doctorDiagnosisRepository.GetEmployeeByEmpIdAsync(empId, userPlantId);
                if (employee == null)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SAVE_EMP_NOTFND", empId, null, null,
                        $"Prescription save failed - employee not found in plant: {userPlantId}");
                    return Json(new { success = false, message = "Employee not found in your plant." });
                }

                // NEW: Validate dependent if not "Self"
                if (selectedDependentName != "Self")
                {
                    var dependentExists = await ValidateDependentAsync(employee.emp_uid, selectedDependentName);
                    if (!dependentExists)
                    {
                        await _auditService.LogAsync("doctor_diagnosis", "SAVE_DEP_NOTFND", empId, null, null,
                            $"Prescription save failed - dependent '{selectedDependentName}' not found for employee: {empId}");
                        return Json(new { success = false, message = $"Dependent '{selectedDependentName}' not found for this employee." });
                    }
                }

                if (selectedDiseases == null || !selectedDiseases.Any())
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SAVE_NODISEASE", empId, null, null,
                        "Prescription save failed - No diseases selected");
                    return Json(new { success = false, message = "Please select at least one disease." });
                }

                // Validate stock before saving with plant filtering
                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await _doctorDiagnosisRepository.GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);
                            if (medicine.Quantity > availableStock)
                            {
                                await _auditService.LogAsync("doctor_diagnosis", "SAVE_NOSTOCK", empId, null, null,
                                    $"Prescription save failed - insufficient stock for {medicine.MedicineName} in plant: {userPlantId}");
                                return Json(new
                                {
                                    success = false,
                                    message = $"Insufficient stock for {medicine.MedicineName} (Batch: {medicine.BatchNo}) in your plant. Available: {availableStock}, Requested: {medicine.Quantity}"
                                });
                            }
                        }
                    }
                }

                var vitalSigns = new VitalSigns
                {
                    BloodPressure = bloodPressure,
                    Pulse = pulse,
                    Temperature = temperature
                };

                var userId = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "anonymous";

                // Save prescription with plant information, patient status, dependent info, and user remarks
                var success = await _doctorDiagnosisRepository.SavePrescriptionAsync(
                    empId, examDate, selectedDiseases, medicines ?? new List<PrescriptionMedicine>(),
                    vitalSigns, userId, userPlantId, selectedVisitType, selectedPatientStatus, selectedDependentName, remarks);

                if (success)
                {
                    _logger.LogInformation($"✅ Prescription saved successfully for {empId} (Patient: {patientInfo}) with stock updates in plant: {userPlantId}, Patient Status: {selectedPatientStatus}");

                    // Log successful prescription save (critical operation) with plant and dependent info
                    await _auditService.LogCreateAsync("doctor_diagnosis", empId,
                        new
                        {
                            EmpId = empId,
                            Patient = patientInfo,
                            DependentName = selectedDependentName,
                            ExamDate = examDate,
                            VisitType = selectedVisitType,
                            PatientStatus = selectedPatientStatus,
                            DiseasesCount = selectedDiseases.Count,
                            MedicinesCount = medicines?.Count ?? 0,
                            PlantId = userPlantId
                        },
                        $"Prescription saved successfully for employee: {empId}, Patient: {patientInfo} in plant: {userPlantId} with patient status: {selectedPatientStatus}");

                    var successMessage = selectedDependentName != "Self"
                        ? $"Prescription saved successfully for dependent: {selectedDependentName}. Medicine stock has been updated."
                        : "Prescription saved successfully. Medicine stock has been updated.";

                    return Json(new { success = true, message = successMessage });
                }
                else
                {
                    await _auditService.LogAsync("doctor_diagnosis", "SAVE_NOEMP", empId, null, null,
                        "Prescription save failed - employee not found or database error");
                    return Json(new { success = false, message = "Failed to save prescription. Please check if the employee exists in your plant." });
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient stock"))
            {
                _logger.LogWarning($"⚠️ Stock validation failed: {ex.Message}");
                await _auditService.LogAsync("doctor_diagnosis", "SAVE_STOCKFAIL", empId ?? "null", null, null,
                    $"Stock validation failed: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error saving prescription for employee {empId}: {ex.Message}");
                await _auditService.LogAsync("doctor_diagnosis", "SAVE_ERROR", empId ?? "null", null, null,
                    $"Prescription save failed with error: {ex.Message}");
                var errorMessage = "Error saving prescription.";
                return Json(new { success = false, message = errorMessage });
            }
        }

        // NEW: Helper method to validate dependent
        private async Task<bool> ValidateDependentAsync(int empUid, string dependentName)
        {
            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var dependent = await dbContext.HrEmployeeDependents
                    .FirstOrDefaultAsync(d => d.emp_uid == empUid &&
                                             d.dep_name == dependentName &&
                                             d.is_active);

                return dependent != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating dependent {dependentName}");
                return false;
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetEmployeeDiagnoses(string empId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log access to employee diagnosis history (sensitive medical data) with plant info
                await _auditService.LogAsync("doctor_diagnosis", "GET_DIAGNOSES", empId ?? "null", null, null,
                    $"Employee diagnosis history access attempted for: {empId}, Plant: {userPlantId}");

                var diagnoses = await _doctorDiagnosisRepository.GetEmployeeDiagnosesAsync(empId, userPlantId);

                // Log successful access with plant info
                await _auditService.LogViewAsync("doctor_diagnosis", empId ?? "null",
                    $"Employee diagnosis history accessed - Count: {diagnoses.Count()}, Plant: {userPlantId}");

                return Json(diagnoses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading diagnoses for employee {empId}");
                await _auditService.LogAsync("doctor_diagnosis", "DIAGN_FAILED", empId ?? "null", null, null,
                    $"Employee diagnosis history access failed: {ex.Message}");
                return Json(new List<DiagnosisEntry>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPrescriptionDetails(int prescriptionId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                _logger.LogInformation($"🔍 Getting prescription details for ID: {prescriptionId}, Plant: {userPlantId}");

                // Log access to sensitive prescription details with plant info
                await _auditService.LogAsync("doctor_diagnosis", "GET_PRESC_DTL", prescriptionId.ToString(), null, null,
                    $"Prescription details access attempted for ID: {prescriptionId}, Plant: {userPlantId}");

                var prescriptionDetails = await _doctorDiagnosisRepository.GetPrescriptionDetailsAsync(prescriptionId, userPlantId);

                if (prescriptionDetails == null)
                {
                    _logger.LogWarning($"❌ Prescription not found for ID: {prescriptionId} in plant: {userPlantId}");
                    await _auditService.LogAsync("doctor_diagnosis", "PRESC_NOTFOUND", prescriptionId.ToString(), null, null,
                        $"Prescription not found for ID: {prescriptionId} in plant: {userPlantId}");
                    return Json(new { success = false, message = "Prescription not found in your plant." });
                }

                // Get user role and apply masking if needed
                var userRole = await GetUserRoleAsync();
                _maskingService.MaskObject(prescriptionDetails, userRole);

                _logger.LogInformation($"✅ Prescription details loaded for ID: {prescriptionId} in plant: {userPlantId}");

                // Log successful access to sensitive prescription data with plant info
                await _auditService.LogViewAsync("doctor_diagnosis", prescriptionId.ToString(),
                    $"Prescription details accessed - Role: {userRole}, Plant: {userPlantId}, Masked: {_maskingService.ShouldMaskData(userRole)}");

                return Json(new
                {
                    success = true,
                    prescription = prescriptionDetails,
                    shouldMaskData = _maskingService.ShouldMaskData(userRole)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error loading prescription details for ID: {prescriptionId}");
                await _auditService.LogAsync("doctor_diagnosis", "PRESC_DTL_FAIL", prescriptionId.ToString(), null, null,
                    $"Prescription details access failed: {ex.Message}");
                return Json(new { success = false, message = "Error loading prescription details." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchEmployeeIds(string term)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log employee ID search attempt with plant info
                await _auditService.LogAsync("doctor_diagnosis", "SEARCH_IDS", "bulk", null, null,
                    $"Employee ID search attempted with term: {term ?? "null"}, Plant: {userPlantId}");

                if (string.IsNullOrWhiteSpace(term))
                {
                    return Json(new List<string>());
                }

                var matchingIds = await _doctorDiagnosisRepository.SearchEmployeeIdsAsync(term, userPlantId);

                // Log search results (without exposing actual IDs in audit) with plant info
                await _auditService.LogAsync("doctor_diagnosis", "SEARCH_IDS_OK", "bulk", null, null,
                    $"Employee ID search completed - Found: {matchingIds.Count()} matches, Plant: {userPlantId}");

                return Json(matchingIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching employee IDs with term: {term}");
                await _auditService.LogAsync("doctor_diagnosis", "SEARCH_IDS_ERR", "bulk", null, null,
                    $"Employee ID search failed: {ex.Message}");
                return Json(new List<string>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingApprovalCount()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log pending approval count access with plant info
                await _auditService.LogAsync("doctor_diagnosis", "GET_PENDING", "system", null, null,
                    $"Pending approval count access attempted for plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("doctor_diagnosis", "PENDING_DENIED", "system", null, null,
                        $"Pending approval count access denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can view pending approvals." });
                }

                var count = await _doctorDiagnosisRepository.GetPendingApprovalCountAsync(userPlantId);

                // Log successful access with plant info
                await _auditService.LogAsync("doctor_diagnosis", "PENDING_OK", "system", null, null,
                    $"Pending approval count accessed - Count: {count}, Plant: {userPlantId}");

                return Json(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approval count");
                await _auditService.LogAsync("doctor_diagnosis", "PENDING_FAIL", "system", null, null,
                    $"Get pending approval count failed: {ex.Message}");
                return Json(new { success = false, count = 0 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PendingApprovals()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log pending approvals access with plant info
                await _auditService.LogAsync("doctor_diagnosis", "PEND_APPR_VIEW", "system", null, null,
                    $"Pending approvals access attempted for plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("doctor_diagnosis", "APPR_DENIED", "system", null, null,
                        $"Pending approvals access denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can view pending approvals." });
                }

                var pendingApprovals = await _doctorDiagnosisRepository.GetPendingApprovalsAsync(userPlantId);

                // No masking needed for doctors viewing pending approvals
                ViewBag.UserRole = userRole;
                ViewBag.UserPlantId = userPlantId;
                ViewBag.ShouldMaskData = false; // Doctors can see all data

                // Log successful access with plant info
                await _auditService.LogAsync("doctor_diagnosis", "APPR_VIEW_OK", "system", null, null,
                    $"Pending approvals accessed - Count: {pendingApprovals.Count()}, Plant: {userPlantId}");

                return PartialView("_PendingApprovalsModal", pendingApprovals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending approvals");
                await _auditService.LogAsync("doctor_diagnosis", "APPR_VIEW_FAIL", "system", null, null,
                    $"Pending approvals access failed: {ex.Message}");
                return Json(new { success = false, message = "Error loading pending approvals." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePrescription(int prescriptionId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log prescription approval attempt (critical operation) with plant info
                await _auditService.LogAsync("doctor_diagnosis", "APPROVE_ATTEMPT", prescriptionId.ToString(), null, null,
                    $"Prescription approval attempted for ID: {prescriptionId}, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("doctor_diagnosis", "APPROVE_DENIED", prescriptionId.ToString(), null, null,
                        $"Prescription approval denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve prescriptions." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "unknown";
                var success = await _doctorDiagnosisRepository.ApprovePrescriptionAsync(prescriptionId, approvedBy, userPlantId);

                if (success)
                {
                    // Log successful approval (critical operation) with plant info
                    await _auditService.LogUpdateAsync("doctor_diagnosis", prescriptionId.ToString(),
                        null, new { Status = "Approved", ApprovedBy = approvedBy, PlantId = userPlantId },
                        $"Prescription approved successfully by: {approvedBy} in plant: {userPlantId}");

                    return Json(new { success = true, message = "Prescription approved successfully." });
                }
                else
                {
                    await _auditService.LogAsync("doctor_diagnosis", "APPROVE_FAILED", prescriptionId.ToString(), null, null,
                        $"Prescription approval failed - not found or already processed in plant: {userPlantId}");
                    return Json(new { success = false, message = "Prescription not found or already processed in your plant." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving prescription {prescriptionId}");
                await _auditService.LogAsync("doctor_diagnosis", "APPROVE_ERROR", prescriptionId.ToString(), null, null,
                    $"Prescription approval failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error approving prescription." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectPrescription(int prescriptionId, string rejectionReason)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log prescription rejection attempt (critical operation) with plant info
                await _auditService.LogAsync("doctor_diagnosis", "REJECT_ATTEMPT", prescriptionId.ToString(), null, null,
                    $"Prescription rejection attempted for ID: {prescriptionId}, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("doctor_diagnosis", "REJECT_DENIED", prescriptionId.ToString(), null, null,
                        $"Prescription rejection denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can reject prescriptions." });
                }

                if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Length < 10)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "REJECT_INVALID", prescriptionId.ToString(), null, null,
                        "Prescription rejection validation failed - insufficient rejection reason");
                    return Json(new { success = false, message = "Please provide a detailed rejection reason (minimum 10 characters)." });
                }

                var rejectedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "unknown";
                var success = await _doctorDiagnosisRepository.RejectPrescriptionAsync(prescriptionId, rejectionReason, rejectedBy, userPlantId);

                if (success)
                {
                    // Log successful rejection (critical operation) with plant info
                    await _auditService.LogUpdateAsync("doctor_diagnosis", prescriptionId.ToString(),
                        null, new { Status = "Rejected", RejectedBy = rejectedBy, RejectionReason = rejectionReason, PlantId = userPlantId },
                        $"Prescription rejected by: {rejectedBy} in plant: {userPlantId}, Reason: {rejectionReason}");

                    return Json(new { success = true, message = "Prescription rejected successfully." });
                }
                else
                {
                    await _auditService.LogAsync("doctor_diagnosis", "REJECT_NOTFOUND", prescriptionId.ToString(), null, null,
                        $"Prescription rejection failed - not found or already processed in plant: {userPlantId}");
                    return Json(new { success = false, message = "Prescription not found or already processed in your plant." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting prescription {prescriptionId}");
                await _auditService.LogAsync("doctor_diagnosis", "REJECT_ERROR", prescriptionId.ToString(), null, null,
                    $"Prescription rejection failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error rejecting prescription." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAllPrescriptions(List<int> prescriptionIds)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log bulk approval attempt (critical operation) with plant info
                await _auditService.LogAsync("doctor_diagnosis", "BULK_APPROVE", "bulk", null, null,
                    $"Bulk prescription approval attempted for {prescriptionIds?.Count ?? 0} prescriptions, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("doctor_diagnosis", "BULK_DENIED", "bulk", null, null,
                        $"Bulk prescription approval denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve prescriptions." });
                }

                if (prescriptionIds == null || !prescriptionIds.Any())
                {
                    await _auditService.LogAsync("doctor_diagnosis", "BULK_INVALID", "bulk", null, null,
                        "Bulk prescription approval validation failed - no prescriptions selected");
                    return Json(new { success = false, message = "No prescriptions selected for approval." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "unknown";
                var approvedCount = await _doctorDiagnosisRepository.ApproveAllPrescriptionsAsync(prescriptionIds, approvedBy, userPlantId);

                if (approvedCount > 0)
                {
                    // Log successful bulk approval (critical operation) with plant info
                    await _auditService.LogUpdateAsync("doctor_diagnosis", "bulk",
                        null, new { ApprovedCount = approvedCount, ApprovedBy = approvedBy, PrescriptionIds = prescriptionIds, PlantId = userPlantId },
                        $"Bulk prescription approval successful - {approvedCount} prescriptions approved by: {approvedBy} in plant: {userPlantId}");

                    return Json(new { success = true, message = $"{approvedCount} prescription(s) approved successfully." });
                }
                else
                {
                    await _auditService.LogAsync("doctor_diagnosis", "BULK_NONE", "bulk", null, null,
                        $"Bulk prescription approval failed - no prescriptions were approved in plant: {userPlantId}");
                    return Json(new { success = false, message = "No prescriptions were approved. They may have been already processed or are not in your plant." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving multiple prescriptions");
                await _auditService.LogAsync("doctor_diagnosis", "BULK_ERROR", "bulk", null, null,
                    $"Bulk prescription approval failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error approving prescriptions." });
            }
        }

        // Helper method to get current user's plant ID (similar to StoreIndentController)
        private async Task<int?> GetCurrentUserPlantIdAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                return await _doctorDiagnosisRepository.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user plant");
                await _auditService.LogAsync("doctor_diagnosis", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        // Helper method to get user role (similar to existing implementation)
        private async Task<string?> GetUserRoleAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var user = await dbContext.SysUsers
                    .Include(u => u.SysRole)
                    .Include(u => u.OrgPlant)
                    .FirstOrDefaultAsync(u => u.full_name == userName || u.email == userName || u.adid == userName);

                return user?.SysRole?.role_name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role");
                await _auditService.LogAsync("doctor_diagnosis", "ROLE_ERROR", "system", null, null,
                    $"Error getting user role: {ex.Message}");
                return null;
            }
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();
                var currentUser = User.FindFirst("user_id")?.Value ??
                                 User.Identity?.Name + " - " + User.GetFullName() ??
                                 "unknown";

                ViewBag.UserRole = userRole;
                ViewBag.UserPlantId = userPlantId;
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                await _auditService.LogAsync("doctor_diagnosis", "EDIT_ATTEMPT", id.ToString(), null, null,
                    $"Prescription edit form access attempted for ID: {id}, Plant: {userPlantId}");

                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_DENIED", id.ToString(), null, null,
                        $"Prescription edit denied - insufficient permissions for role: {userRole}");
                    TempData["Error"] = "You don't have permission to edit prescriptions.";
                    return RedirectToAction(nameof(Index));
                }

                var permissionResult = await _doctorDiagnosisRepository.CanEditPrescriptionAsync(id, userPlantId);
                if (!permissionResult.CanEdit)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_NOTALLOWED", id.ToString(), null, null,
                        $"Prescription edit not allowed: {permissionResult.Message}");
                    TempData["Error"] = permissionResult.Message;
                    return RedirectToAction(nameof(Index));
                }

                var editModel = await _doctorDiagnosisRepository.GetPrescriptionForEditAsync(id, userPlantId);
                if (editModel == null)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_NOTFOUND", id.ToString(), null, null,
                        $"Prescription not found for edit in plant: {userPlantId}");
                    TempData["Error"] = "Prescription not found or cannot be edited.";
                    return RedirectToAction(nameof(Index));
                }

                // NEW: Check if user is creator or doctor
                var isCreator = editModel.CreatedBy == currentUser;
                var isDoctor = userRole?.ToLower() == "doctor";

                if (!isCreator && !isDoctor)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_UNAUTHORIZED", id.ToString(), null, null,
                        $"User {currentUser} not authorized to edit prescription created by {editModel.CreatedBy}");
                    TempData["Error"] = "You can only edit your own prescriptions or if you're a doctor.";
                    return RedirectToAction(nameof(Index));
                }

                _maskingService.MaskObject(editModel, userRole);

                await _auditService.LogViewAsync("doctor_diagnosis", id.ToString(),
                    $"Prescription edit form accessed - Employee: {editModel.EmployeeName}, Status: {editModel.ApprovalStatus}, Plant: {userPlantId}");

                return View(editModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for prescription {id}");
                await _auditService.LogAsync("doctor_diagnosis", "EDIT_ERROR", id.ToString(), null, null,
                    $"Edit form load failed: {ex.Message}");
                TempData["Error"] = "Error loading prescription for editing.";
                return RedirectToAction(nameof(Index));
            }
        }
        

        [HttpPost]
        public async Task<IActionResult> Edit(int prescriptionId, List<int>? selectedDiseases,
        List<PrescriptionMedicine>? medicines, string? bloodPressure, string? pulse,
        string? temperature, string? visitType = null, string? patientStatus = null,
        string? dependentName = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();

                // Log edit save attempt
                await _auditService.LogAsync("doctor_diagnosis", "EDIT_SAVE_ATTEMPT", prescriptionId.ToString(), null, null,
                    $"Prescription edit save attempted for ID: {prescriptionId}, Plant: {userPlantId}");

                // Check permissions
                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_SAVE_DENIED", prescriptionId.ToString(), null, null,
                        $"Prescription edit save denied for role: {userRole}");
                    return Json(new { success = false, message = "You don't have permission to edit prescriptions." });
                }

                // Check if can still edit
                var permissionResult = await _doctorDiagnosisRepository.CanEditPrescriptionAsync(prescriptionId, userPlantId);
                if (!permissionResult.CanEdit)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_SAVE_NOTALLOWED", prescriptionId.ToString(), null, null,
                        $"Prescription edit save not allowed: {permissionResult.Message}");
                    return Json(new { success = false, message = permissionResult.Message });
                }

                // Get existing prescription to preserve data if not provided
                var existingPrescription = await _doctorDiagnosisRepository.GetPrescriptionForEditAsync(prescriptionId, userPlantId);
                if (existingPrescription == null)
                {
                    return Json(new { success = false, message = "Prescription not found." });
                }

                // If diseases not provided, use existing
                if (selectedDiseases?.Any() != true)
                {
                    selectedDiseases = existingPrescription.SelectedDiseaseIds ?? new List<int>();
                }

                // If medicines not provided, convert existing to PrescriptionMedicine format
                if (medicines?.Any() != true)
                {
                    medicines = existingPrescription.CurrentMedicines?.Select(m => new PrescriptionMedicine
                    {
                        MedItemId = m.MedItemId,
                        Quantity = m.Quantity,
                        Dose = m.Dose,
                        MedicineName = m.MedicineName,
                        IndentItemId = null, // Will be resolved in repository
                        BatchNo = null,
                        ExpiryDate = null,
                        AvailableStock = 0
                    }).ToList() ?? new List<PrescriptionMedicine>();
                }

                // Validate input
                if (selectedDiseases?.Any() != true)
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_SAVE_NODISEASE", prescriptionId.ToString(), null, null,
                        "Edit save failed - No diseases selected");
                    return Json(new { success = false, message = "Please select at least one disease." });
                }

                var vitalSigns = new VitalSigns
                {
                    BloodPressure = bloodPressure ?? existingPrescription.BloodPressure,
                    Pulse = pulse ?? existingPrescription.Pulse,
                    Temperature = temperature ?? existingPrescription.Temperature
                };

                var userId = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "anonymous";
                var selectedVisitType = visitType ?? existingPrescription.VisitType;
                var selectedPatientStatus = patientStatus ?? existingPrescription.PatientStatus;
                var selectedDependentName = dependentName ?? ExtractDependentNameFromRemarks(existingPrescription.Remarks) ?? "Self";

                // Update prescription
                var updateResult = await _doctorDiagnosisRepository.UpdatePrescriptionAsync(
                    prescriptionId, selectedDiseases, medicines,
                    vitalSigns, userId, userPlantId, selectedVisitType, selectedPatientStatus,
                    selectedDependentName);

                if (updateResult.Success)
                {
                    // Log successful update
                    await _auditService.LogUpdateAsync("doctor_diagnosis", prescriptionId.ToString(),
                        null, new
                        {
                            DiseasesCount = selectedDiseases.Count,
                            MedicinesCount = medicines?.Count ?? 0,
                            ModifiedBy = userId,
                            PlantId = userPlantId,
                            PatientStatus = selectedPatientStatus,
                            DependentName = selectedDependentName,
                            StockAdjusted = updateResult.StockAdjusted,
                            AffectedMedicines = updateResult.AffectedMedicines
                        },
                        $"Prescription updated successfully by: {userId} in plant: {userPlantId}");

                    return Json(new
                    {
                        success = true,
                        message = updateResult.Message,
                        redirectUrl = Url.Action("Index")
                    });
                }
                else
                {
                    await _auditService.LogAsync("doctor_diagnosis", "EDIT_SAVE_FAILED", prescriptionId.ToString(), null, null,
                        $"Edit save failed: {updateResult.Message}");

                    return Json(new
                    {
                        success = false,
                        message = updateResult.Message,
                        validationErrors = updateResult.ValidationErrors
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating prescription {prescriptionId}");
                await _auditService.LogAsync("doctor_diagnosis", "EDIT_SAVE_ERROR", prescriptionId.ToString(), null, null,
                    $"Edit save error: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "Error updating prescription: " + ex.Message
                });
            }
        }

        // Helper method to extract dependent name from remarks
        private string? ExtractDependentNameFromRemarks(string? remarks)
        {
            if (string.IsNullOrEmpty(remarks)) return "Self";

            try
            {
                if (remarks.Contains("Patient: Dependent -", StringComparison.OrdinalIgnoreCase))
                {
                    var startIndex = remarks.IndexOf("Patient: Dependent -", StringComparison.OrdinalIgnoreCase);
                    if (startIndex >= 0)
                    {
                        var afterPrefix = remarks.Substring(startIndex + "Patient: Dependent -".Length).Trim();
                        var endIndex = afterPrefix.IndexOf(';');
                        if (endIndex > 0)
                        {
                            return afterPrefix.Substring(0, endIndex).Trim();
                        }
                        else
                        {
                            return afterPrefix.Trim();
                        }
                    }
                }
                return "Self";
            }
            catch
            {
                return "Self";
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetPrescriptionEditData(int prescriptionId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log access to edit data
                await _auditService.LogAsync("doctor_diagnosis", "GET_EDIT_DATA", prescriptionId.ToString(), null, null,
                    $"Prescription edit data access attempted for ID: {prescriptionId}, Plant: {userPlantId}");

                // Check permissions
                var permissionResult = await _doctorDiagnosisRepository.CanEditPrescriptionAsync(prescriptionId, userPlantId);
                if (!permissionResult.CanEdit)
                {
                    return Json(new { success = false, message = permissionResult.Message });
                }

                // Get prescription data
                var editModel = await _doctorDiagnosisRepository.GetPrescriptionForEditAsync(prescriptionId, userPlantId);
                if (editModel == null)
                {
                    return Json(new { success = false, message = "Prescription not found." });
                }

                // Get fresh prescription data for editing
                var prescriptionData = await GetPrescriptionDataForEdit(userPlantId);

                // Log successful access
                await _auditService.LogAsync("doctor_diagnosis", "EDIT_DATA_OK", prescriptionId.ToString(), null, null,
                    $"Prescription edit data loaded successfully for plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    prescription = new
                    {
                        prescriptionId = editModel.PrescriptionId,
                        employeeId = editModel.EmployeeId,
                        employeeName = editModel.EmployeeName,
                        selectedDiseaseIds = editModel.SelectedDiseaseIds,
                        bloodPressure = editModel.BloodPressure,
                        pulse = editModel.Pulse,
                        temperature = editModel.Temperature,
                        patientStatus = editModel.PatientStatus,
                        currentMedicines = await GetCurrentMedicinesWithStockInfoAsync(editModel.CurrentMedicines, userPlantId, editModel.ApprovalStatus)
                    },
                    diseases = prescriptionData.diseases,
                    medicines = prescriptionData.medicines
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting prescription edit data for {prescriptionId}");
                await _auditService.LogAsync("doctor_diagnosis", "EDIT_DATA_FAIL", prescriptionId.ToString(), null, null,
                    $"Get edit data failed: {ex.Message}");

                return Json(new { success = false, message = "Error loading prescription data." });
            }
        }

        private async Task<dynamic> GetPrescriptionDataForEdit(int? userPlantId)
        {
            // Get plant-wise diseases
            var diseases = await _doctorDiagnosisRepository.GetDiseasesAsync(userPlantId);

            // Get plant-wise medicines with batch information
            var medicineStocks = await _doctorDiagnosisRepository.GetMedicinesFromCompounderIndentAsync(userPlantId);

            // Convert medicines to dropdown format
            var medicineDropdownItems = medicineStocks
                .Where(m => m.AvailableStock > 0)
                .OrderBy(m => m.ExpiryDate)
                .ThenBy(m => m.MedItemName)
                .ThenBy(m => m.BatchNo)
                .Select(m => new
                {
                    indentItemId = m.IndentItemId,
                    medItemId = m.MedItemId,
                    baseName = m.BaseName,
                    //text = $"{m.MedItemId} - {m.BaseName} - {m.MedItemName} | Batch: {m.BatchNo}",
                    text = $"{m.MedItemId} - {(string.IsNullOrEmpty(m.BaseName) || m.BaseName == "Not Defined" ? "" : $"{m.BaseName} - ")}{m.MedItemName} | Batch: {m.BatchNo}",
                    stockInfo = $"Stock: {m.AvailableStock}",
                    expiryInfo = m.ExpiryDateFormatted,
                    daysToExpiry = m.DaysToExpiry,
                    availableStock = m.AvailableStock,
                    batchNo = m.BatchNo,
                    expiryDate = m.ExpiryDate?.ToString("yyyy-MM-dd"),
                    companyName = m.CompanyName,
                    plantId = m.PlantId,
                    medicineName = m.MedItemName,
                    expiryClass = m.DaysToExpiry switch
                    {
                        < 0 => "text-danger",
                        <= 7 => "text-warning",
                        <= 30 => "text-info",
                        _ => "text-success"
                    },
                    expiryLabel = m.DaysToExpiry switch
                    {
                        < 0 => "EXPIRED",
                        <= 7 => $"{m.ExpiryDate?.ToString("yyyy-MM-dd")} - ({m.DaysToExpiry}d)",
                        <= 30 => $"{m.ExpiryDate?.ToString("yyyy-MM-dd")} - ({m.DaysToExpiry}d)",
                        _ => $"Expires: {m.ExpiryDateFormatted}"
                    }
                })
                .ToList();

            // Convert diseases to dropdown format
            var diseaseDropdownItems = diseases.Select(d => new {
                value = d.DiseaseId,
                text = $"{d.DiseaseId} - {d.DiseaseName}",
                description = d.DiseaseDesc ?? ""
            });

            return new
            {
                diseases = diseaseDropdownItems,
                medicines = medicineDropdownItems
            };
        }

        private async Task<List<dynamic>> GetCurrentMedicinesWithStockInfoAsync(
            List<PrescriptionMedicineEdit> currentMedicines, int? userPlantId, string approvalStatus)
        {
            var result = new List<dynamic>();

            if (currentMedicines?.Any() != true)
                return result;

            try
            {
                _logger.LogInformation($"Getting stock info for {currentMedicines.Count} existing medicines in plant {userPlantId}");

                // Get all available medicine stocks for the plant
                var medicineStocks = await _doctorDiagnosisRepository.GetMedicinesFromCompounderIndentAsync(userPlantId);

                _logger.LogInformation($"Found {medicineStocks.Count} total medicine stock records");

                foreach (var medicine in currentMedicines)
                {
                    _logger.LogInformation($"Looking for stock info for medicine ID: {medicine.MedItemId}, Name: {medicine.MedicineName}");

                    // Find ALL matching stock records for this medicine (not just available stock > 0)
                    var allMatchingStocks = medicineStocks
                        .Where(stock => stock.MedItemId == medicine.MedItemId)
                        .ToList();

                    _logger.LogInformation($"Found {allMatchingStocks.Count} stock records for medicine ID {medicine.MedItemId}");

                    // Try to find available stock first (FIFO - earliest expiry first)
                    var matchingStock = allMatchingStocks
                        .Where(stock => stock.AvailableStock > 0)
                        .OrderBy(stock => stock.ExpiryDate ?? DateTime.MaxValue)
                        .ThenBy(stock => stock.BatchNo)
                        .FirstOrDefault();

                    // If no available stock, try to get any stock record for display info
                    if (matchingStock == null)
                    {
                        matchingStock = allMatchingStocks
                            .OrderBy(stock => stock.ExpiryDate ?? DateTime.MaxValue)
                            .ThenBy(stock => stock.BatchNo)
                            .FirstOrDefault();

                        _logger.LogWarning($"No available stock for medicine ID {medicine.MedItemId}, using first stock record for info");
                    }

                    if (matchingStock != null)
                    {
                        // Add the current prescription quantity back to available stock for display
                        var displayStock = 0;
                        if (approvalStatus != "Rejected")
                        {
                            displayStock = matchingStock.AvailableStock + medicine.Quantity;
                        }
                        else
                        {
                            displayStock = matchingStock.AvailableStock;
                        }


                        _logger.LogInformation($"Medicine ID {medicine.MedItemId}: Stock {matchingStock.AvailableStock} + Current {medicine.Quantity} = Display {displayStock}");

                        result.Add(new
                        {
                            medItemId = medicine.MedItemId,
                            medicineName = medicine.MedicineName,
                            baseName = medicine.BaseName,
                            quantity = medicine.Quantity,
                            dose = medicine.Dose,
                            companyName = medicine.CompanyName,
                            // Real stock and expiry information
                            indentItemId = matchingStock.IndentItemId,
                            availableStock = displayStock,
                            batchNo = matchingStock.BatchNo ?? "N/A",
                            expiryDate = matchingStock.ExpiryDate?.ToString("yyyy-MM-dd") ?? "",
                            expiryDateFormatted = matchingStock.ExpiryDateFormatted ?? "N/A",
                            daysToExpiry = matchingStock.DaysToExpiry,
                            expiryClass = matchingStock.DaysToExpiry switch
                            {
                                < 0 => "text-danger",
                                <= 7 => "text-warning",
                                <= 30 => "text-info",
                                _ => "text-success"
                            },
                            expiryLabel = matchingStock.DaysToExpiry switch
                            {
                                < 0 => "EXPIRED",
                                <= 7 => $"{matchingStock.ExpiryDate?.ToString("yyyy-MM-dd")} - ({matchingStock.DaysToExpiry}d)",
                                <= 30 => $"{matchingStock.ExpiryDate?.ToString("yyyy-MM-dd")} - ({matchingStock.DaysToExpiry}d)",
                                _ => $"Expires: {matchingStock.ExpiryDateFormatted}"
                            }
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"No stock records found for medicine ID {medicine.MedItemId} - {medicine.MedicineName}");

                        // Alternative: Try to get basic medicine info from MedMaster
                        result.Add(await GetBasicMedicineInfoAsync(medicine, userPlantId));
                    }
                }

                _logger.LogInformation($"Completed stock info lookup - {result.Count} medicines processed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock info for existing medicines");

                // Return basic info without stock data in case of error
                return currentMedicines.Select(m => new
                {
                    medItemId = m.MedItemId,
                    medicineName = m.MedicineName,
                    baseName = m.BaseName,
                    quantity = m.Quantity,
                    dose = m.Dose,
                    companyName = m.CompanyName,
                    indentItemId = 0,
                    availableStock = 0,
                    batchNo = "Error",
                    expiryDate = "",
                    expiryDateFormatted = "Error loading",
                    daysToExpiry = -999,
                    expiryClass = "text-danger",
                    expiryLabel = "Error loading stock"
                }).Cast<dynamic>().ToList();
            }
        }

        private async Task<dynamic> GetBasicMedicineInfoAsync(PrescriptionMedicineEdit medicine, int? userPlantId)
        {
            try
            {
                // Get basic medicine info directly from database
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var medMaster = await dbContext.med_masters
                    .Include(m => m.MedBase)
                    .FirstOrDefaultAsync(m => m.MedItemId == medicine.MedItemId);

                if (medMaster != null)
                {
                    return new
                    {
                        medItemId = medicine.MedItemId,
                        medicineName = medicine.MedicineName,
                        baseName = medicine.BaseName,
                        quantity = medicine.Quantity,
                        dose = medicine.Dose,
                        companyName = medMaster.CompanyName,
                        indentItemId = 0,
                        availableStock = 0, // No stock info available
                        batchNo = "N/A",
                        expiryDate = "",
                        expiryDateFormatted = "No stock record",
                        daysToExpiry = -999,
                        expiryClass = "text-warning",
                        expiryLabel = "No current stock"
                    };
                }
                else
                {
                    return new
                    {
                        medItemId = medicine.MedItemId,
                        medicineName = medicine.MedicineName,
                        baseName = medicine.BaseName,
                        quantity = medicine.Quantity,
                        dose = medicine.Dose,
                        companyName = medicine.CompanyName,
                        indentItemId = 0,
                        availableStock = 0,
                        batchNo = "N/A",
                        expiryDate = "",
                        expiryDateFormatted = "Medicine not found",
                        daysToExpiry = -999,
                        expiryClass = "text-danger",
                        expiryLabel = "Medicine not found"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting basic medicine info for {medicine.MedItemId}");

                return new
                {
                    medItemId = medicine.MedItemId,
                    medicineName = medicine.MedicineName,
                    baseName = medicine.BaseName,
                    quantity = medicine.Quantity,
                    dose = medicine.Dose,
                    companyName = medicine.CompanyName,
                    indentItemId = 0,
                    availableStock = 0,
                    batchNo = "Error",
                    expiryDate = "",
                    expiryDateFormatted = "Error",
                    daysToExpiry = -999,
                    expiryClass = "text-danger",
                    expiryLabel = "Error loading"
                };
            }
        }
    }
}