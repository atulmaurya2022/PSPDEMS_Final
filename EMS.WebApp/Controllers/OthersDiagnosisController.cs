using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessOthersDiagnosis")]
    public class OthersDiagnosisController : Controller
    {
        private readonly IOthersDiagnosisRepository _repository;
        private readonly IMedicalDataMaskingService _maskingService;
        private readonly IAuditService _auditService;
        private readonly ILogger<OthersDiagnosisController> _logger;

        public OthersDiagnosisController(
            IOthersDiagnosisRepository repository,
            IMedicalDataMaskingService maskingService,
            IAuditService auditService,
            ILogger<OthersDiagnosisController> logger)
        {
            _repository = repository;
            _maskingService = maskingService;
            _auditService = auditService;
            _logger = logger;
        }

        // GET: OthersDiagnosis
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

                await _auditService.LogAsync("others_diagnosis", "INDEX_VIEW", "main", null, null,
                    $"Others diagnosis module accessed by user role: {userRole}, Plant: {userPlantId}");

                var diagnoses = await _repository.GetAllDiagnosesAsync(userPlantId);

                await _auditService.LogAsync("others_diagnosis", "INDEX_SUCCESS", "main", null, null,
                    $"Others diagnosis list loaded - Count: {diagnoses.Count()}, Role: {userRole}, Plant: {userPlantId}");

                return View(diagnoses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading others diagnosis list");
                await _auditService.LogAsync("others_diagnosis", "INDEX_FAILED", "main", null, null,
                    $"Failed to load others diagnosis index: {ex.Message}");
                TempData["Error"] = "Error loading diagnosis records.";
                return View(new List<OthersDiagnosisListViewModel>());
            }
        }
        //public async Task<IActionResult> Index()
        //{
        //    try
        //    {
        //        // NEW: Get user's plant ID for filtering
        //        var userPlantId = await GetCurrentUserPlantIdAsync();

        //        // Pass user role to view for client-side masking
        //        var userRole = await GetUserRoleAsync();
        //        ViewBag.UserRole = userRole;
        //        ViewBag.IsDoctor = userRole?.ToLower() == "doctor";
        //        ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

        //        // Log index access for security monitoring
        //        await _auditService.LogAsync("others_diagnosis", "INDEX_VIEW", "main", null, null,
        //            $"Others diagnosis module accessed by user role: {userRole}, Plant: {userPlantId}");

        //        // NEW: Pass plant filtering to repository
        //        var diagnoses = await _repository.GetAllDiagnosesAsync(userPlantId);

        //        // Log successful data loading
        //        await _auditService.LogAsync("others_diagnosis", "INDEX_SUCCESS", "main", null, null,
        //            $"Others diagnosis list loaded - Count: {diagnoses.Count()}, Role: {userRole}, Plant: {userPlantId}");

        //        return View(diagnoses);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading others diagnosis list");
        //        await _auditService.LogAsync("others_diagnosis", "INDEX_FAILED", "main", null, null,
        //            $"Failed to load others diagnosis index: {ex.Message}");
        //        TempData["Error"] = "Error loading diagnosis records.";
        //        return View(new List<OthersDiagnosisListViewModel>());
        //    }
        //}

        public async Task<IActionResult> Add()
        {
            try
            {
                // Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Validate plant assignment
                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("others_diagnosis", "ADD_NO_PLANT", "new", null, null,
                        "Add form access denied - user has no plant assigned");
                    TempData["Error"] = "User is not assigned to any plant. Please contact administrator.";
                    return RedirectToAction(nameof(Index));
                }

                // Log add form access
                await _auditService.LogAsync("others_diagnosis", "ADD_FORM", "main", null, null,
                    $"Others diagnosis add form accessed, Plant: {userPlantId}");

                // Check user role and apply masking
                var userRole = await GetUserRoleAsync();
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);
                ViewBag.UserRole = userRole;

                string newTreatmentId;
                try
                {
                    // Try to auto-generate TreatmentId
                    newTreatmentId = await _repository.GenerateNewTreatmentIdAsync();
                    _logger.LogInformation("Successfully generated Treatment ID: {TreatmentId}", newTreatmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate Treatment ID, using fallback");
                    // Fallback to a default ID with timestamp to ensure uniqueness
                    newTreatmentId = $"T{DateTime.Now:HHmmss}";

                    // Log the fallback usage
                    await _auditService.LogAsync("others_diagnosis", "TREATMENTID_FALLBACK", "system", null, null,
                        $"TreatmentId generation failed, using fallback: {newTreatmentId}");
                }

                var model = new OthersDiagnosisViewModel
                {
                    TreatmentId = newTreatmentId, // Auto-generated TreatmentId
                    DiagnosedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "SYSTEM ADMIN",
                    VisitType = "Regular Visitor", // Default visit type
                                                   // UPDATED: Get diseases filtered by plant
                    AvailableDiseases = await _repository.GetDiseasesAsync(userPlantId),
                    // Get medicines filtered by plant with batch grouping
                    AvailableMedicines = await _repository.GetCompounderMedicinesAsync()
                };

                // Log successful form loading
                await _auditService.LogAsync("others_diagnosis", "ADD_FORM_OK", "main", null, null,
                    $"Add form loaded successfully - Role: {userRole}, TreatmentId: {newTreatmentId}, Diseases: {model.AvailableDiseases.Count}, Medicines: {model.AvailableMedicines.Count}, Plant: {userPlantId}");

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading add diagnosis form");
                await _auditService.LogAsync("others_diagnosis", "ADD_FORM_FAIL", "main", null, null,
                    $"Add form loading failed: {ex.Message}");
                TempData["Error"] = "Error loading form. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(OthersDiagnosisViewModel model)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // NEW: Validate plant assignment
                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("others_diagnosis", "SAVE_NO_PLANT", model.TreatmentId ?? "null", null, null,
                        "Save denied - user has no plant assigned");
                    TempData["Error"] = "User is not assigned to any plant. Please contact administrator.";
                    return RedirectToAction(nameof(Index));
                }

                // Log save attempt (critical operation)
                await _auditService.LogAsync("others_diagnosis", "SAVE_ATTEMPT", model.TreatmentId ?? "null", null, null,
                    $"Others diagnosis save attempted - TreatmentId: {model.TreatmentId}, Patient: {model.PatientName}, VisitType: {model.VisitType}, Plant: {userPlantId}");

                // Check permissions for saving
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("others_diagnosis", "SAVE_DENIED", model.TreatmentId ?? "null", null, null,
                        $"Others diagnosis save denied - insufficient permissions for role: {userRole}");
                    TempData["Error"] = "You don't have permission to save diagnoses.";
                    return RedirectToAction(nameof(Index));
                }

                if (!ModelState.IsValid)
                {
                    await _auditService.LogAsync("others_diagnosis", "SAVE_INVALID", model.TreatmentId ?? "null", null, null,
                        "Others diagnosis save failed - model validation failed");
                    model.AvailableDiseases = await _repository.GetDiseasesAsync();
                    model.AvailableMedicines = await _repository.GetMedicinesAsync();
                    return View(model);
                }

                var createdBy = User.Identity?.Name + " - " + User.GetFullName() ?? "SYSTEM ADMIN";
                // NEW: Pass user's plant ID to repository
                var (success, errorMessage) = await _repository.SaveDiagnosisAsync(model, createdBy, userPlantId);

                if (success)
                {
                    // Log successful save (critical operation)
                    await _auditService.LogCreateAsync("others_diagnosis", model.TreatmentId ?? "unknown",
                        new { TreatmentId = model.TreatmentId, PatientName = model.PatientName, VisitType = model.VisitType, DiseasesCount = model.SelectedDiseaseIds?.Count ?? 0, MedicinesCount = model.PrescriptionMedicines?.Count ?? 0, PlantId = userPlantId },
                        $"Others diagnosis saved successfully - Patient: {model.PatientName}, CreatedBy: {createdBy}, Plant: {userPlantId}");

                    TempData["Success"] = errorMessage; // Contains appropriate message based on approval status
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "SAVE_FAILED", model.TreatmentId ?? "null", null, null,
                        $"Others diagnosis save failed: {errorMessage}");
                    ModelState.AddModelError("", errorMessage);
                    model.AvailableDiseases = await _repository.GetDiseasesAsync();
                    model.AvailableMedicines = await _repository.GetMedicinesAsync();
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving diagnosis");
                await _auditService.LogAsync("others_diagnosis", "SAVE_ERROR", model.TreatmentId ?? "null", null, null,
                    $"Others diagnosis save failed with error: {ex.Message}");
                ModelState.AddModelError("", $"An error occurred while saving: {ex.Message}");
                model.AvailableDiseases = await _repository.GetDiseasesAsync();
                model.AvailableMedicines = await _repository.GetMedicinesAsync();
                return View(model);
            }
        }

        // POST: OthersDiagnosis/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();
                var currentUser = User.FindFirst("user_id")?.Value ??
                                 User.Identity?.Name + " - " + User.GetFullName() ??
                                 "unknown";

                await _auditService.LogAsync("others_diagnosis", "DELETE_ATTEMPT", id.ToString(), null, null,
                    $"Others diagnosis deletion attempted for ID: {id}, Plant: {userPlantId}");

                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("others_diagnosis", "DELETE_DENIED", id.ToString(), null, null,
                        $"Others diagnosis deletion denied - insufficient permissions for role: {userRole}");
                    TempData["Error"] = "You don't have permission to delete diagnoses.";
                    return RedirectToAction(nameof(Index));
                }

                // NEW: Get diagnosis details to check creator
                var diagnosisDetails = await _repository.GetDiagnosisDetailsAsync(id, userPlantId);

                if (diagnosisDetails == null)
                {
                    TempData["Error"] = "Diagnosis not found or access denied.";
                    return RedirectToAction(nameof(Index));
                }

                // NEW: Check if user is creator or doctor
                var isCreator = diagnosisDetails.DiagnosedBy == currentUser;
                var isDoctor = userRole?.ToLower() == "doctor";

                if (!isCreator && !isDoctor)
                {
                    await _auditService.LogAsync("others_diagnosis", "DELETE_UNAUTHORIZED", id.ToString(), null, null,
                        $"User {currentUser} not authorized to delete diagnosis created by {diagnosisDetails.DiagnosedBy}");
                    TempData["Error"] = "You can only delete your own diagnoses or if you're a doctor.";
                    return RedirectToAction(nameof(Index));
                }

                var success = await _repository.DeleteDiagnosisAsync(id, userPlantId);
                if (success)
                {
                    await _auditService.LogUpdateAsync("others_diagnosis", id.ToString(),
                        null, new { Status = "Deleted", DeletedBy = currentUser, PlantId = userPlantId },
                        $"Others diagnosis deleted successfully by: {currentUser}, Plant: {userPlantId}");

                    TempData["Success"] = "Diagnosis record deleted successfully.";
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "DELETE_NOTFOUND", id.ToString(), null, null,
                        "Others diagnosis deletion failed - record not found or access denied");
                    TempData["Error"] = "Failed to delete diagnosis record.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting diagnosis with ID: {id}");
                await _auditService.LogAsync("others_diagnosis", "DELETE_ERROR", id.ToString(), null, null,
                    $"Others diagnosis deletion failed with error: {ex.Message}");
                TempData["Error"] = "Error deleting diagnosis record.";
                return RedirectToAction(nameof(Index));
            }
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Delete(int id)
        //{
        //    try
        //    {
        //        // NEW: Get user's plant ID
        //        var userPlantId = await GetCurrentUserPlantIdAsync();

        //        // Log delete attempt (critical operation)
        //        await _auditService.LogAsync("others_diagnosis", "DELETE_ATTEMPT", id.ToString(), null, null,
        //            $"Others diagnosis deletion attempted for ID: {id}, Plant: {userPlantId}");

        //        // Check permissions for deletion
        //        var userRole = await GetUserRoleAsync();
        //        if (_maskingService.ShouldMaskData(userRole))
        //        {
        //            await _auditService.LogAsync("others_diagnosis", "DELETE_DENIED", id.ToString(), null, null,
        //                $"Others diagnosis deletion denied - insufficient permissions for role: {userRole}");
        //            TempData["Error"] = "You don't have permission to delete diagnoses.";
        //            return RedirectToAction(nameof(Index));
        //        }

        //        // NEW: Pass plant filtering to repository
        //        var success = await _repository.DeleteDiagnosisAsync(id, userPlantId);
        //        if (success)
        //        {
        //            // Log successful deletion (critical operation)
        //            await _auditService.LogUpdateAsync("others_diagnosis", id.ToString(),
        //                null, new { Status = "Deleted", DeletedBy = User.Identity?.Name ?? "unknown", PlantId = userPlantId },
        //                $"Others diagnosis deleted successfully by: {User.Identity?.Name ?? "unknown"}, Plant: {userPlantId}");

        //            TempData["Success"] = "Diagnosis record deleted successfully.";
        //        }
        //        else
        //        {
        //            await _auditService.LogAsync("others_diagnosis", "DELETE_NOTFOUND", id.ToString(), null, null,
        //                "Others diagnosis deletion failed - record not found or access denied");
        //            TempData["Error"] = "Failed to delete diagnosis record or access denied.";
        //        }

        //        return RedirectToAction(nameof(Index));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error deleting diagnosis with ID: {id}");
        //        await _auditService.LogAsync("others_diagnosis", "DELETE_ERROR", id.ToString(), null, null,
        //            $"Others diagnosis deletion failed with error: {ex.Message}");
        //        TempData["Error"] = "Error deleting diagnosis record.";
        //        return RedirectToAction(nameof(Index));
        //    }
        //}

        // AJAX Methods

        [HttpGet]
        public async Task<IActionResult> SearchPatient(string treatmentId)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log search attempt
                await _auditService.LogAsync("others_diagnosis", "SEARCH_PATIENT", treatmentId ?? "null", null, null,
                    $"Patient search attempted - TreatmentId: {treatmentId}, Plant: {userPlantId}");

                if (string.IsNullOrWhiteSpace(treatmentId))
                {
                    await _auditService.LogAsync("others_diagnosis", "SEARCH_INVALID", "null", null, null,
                        "Patient search failed - Treatment ID is required");
                    return Json(new { success = false, message = "Treatment ID is required." });
                }

                // NEW: Pass plant filtering to repository
                var patientData = await _repository.GetPatientForEditAsync(treatmentId, userPlantId);
                if (patientData == null)
                {
                    await _auditService.LogAsync("others_diagnosis", "SEARCH_NOTFOUND", treatmentId, null, null,
                        $"Patient not found for Treatment ID: {treatmentId} or access denied for plant: {userPlantId}");
                    return Json(new { success = false, message = "Patient not found or access denied." });
                }

                // Log successful search
                await _auditService.LogAsync("others_diagnosis", "SEARCH_SUCCESS", treatmentId, null, null,
                    $"Patient found - Name: {patientData.PatientName}, Age: {patientData.Age}, Plant: {patientData.PlantName}");

                return Json(new { success = true, patient = patientData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for patient with Treatment ID: {treatmentId}");
                await _auditService.LogAsync("others_diagnosis", "SEARCH_ERROR", treatmentId ?? "null", null, null,
                    $"Patient search failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error searching for patient." });
            }
        }

        // ======= UPDATED: Enhanced prescription data with batch information and stock WITH PLANT FILTERING =======
        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> GetPrescriptionData()
        {
            try
            {
                // Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                _logger.LogInformation("Getting prescription data with FIFO batch selection for Others Diagnosis, Plant: {PlantId}", userPlantId);

                // Log access to prescription/medicine data
                await _auditService.LogAsync("others_diagnosis", "GET_PRESCDATA", "system", null, null,
                    $"Prescription and medicine data access attempted, Plant: {userPlantId}");

                // UPDATED: Get diseases filtered by plant
                var diseases = await _repository.GetDiseasesAsync(userPlantId);
                // Get medicines filtered by plant with FIFO logic (no grouping - keep each IndentItemId-Batch separate)
                var medicineStocks = await _repository.GetMedicinesFromCompounderIndentAsync(userPlantId);

                _logger.LogInformation($"Found {diseases.Count} diseases and {medicineStocks.Count} individual medicine batches for plant {userPlantId}");

                // FIXED: Convert to dropdown format - Keep each IndentItemId-Batch combination separate
                var medicineDropdownItems = medicineStocks
                    .Where(m => m.AvailableStock > 0) // Only show items with available stock
                    .OrderBy(m => m.ExpiryDate) // Order by medicine name first
                    .ThenBy(m => m.MedItemName)
                    .ThenBy(m => m.BatchNo) // Then by batch number
                                            //.ThenBy(m => m.ExpiryDate ?? DateTime.MaxValue) // Then by expiry date (FIFO)
                    .Select(m => new
                    {
                        indentItemId = m.IndentItemId, // FIXED: Use actual IndentItemId, not grouped
                        medItemId = m.MedItemId,
                        baseName = m.BaseName,
                        medItemName = m.MedItemName,
                        // FIXED: Better display format - ID - BaseName - MedItemName | Batch
                        text = $"{m.MedItemId} - {m.BaseName} - {m.MedItemName} | Batch: {m.BatchNo}",
                        stockInfo = $"Stock: {m.AvailableStock}",
                        expiryInfo = m.ExpiryDateFormatted,
                        daysToExpiry = m.DaysToExpiry,
                        availableStock = m.AvailableStock, // FIXED: Use actual stock for this specific batch
                        batchNo = m.BatchNo,
                        expiryDate = m.ExpiryDate?.ToString("yyyy-MM-dd"),
                        companyName = m.CompanyName,
                        // Add styling classes based on expiry
                        expiryClass = m.DaysToExpiry switch
                        {
                            < 0 => "text-danger", // Expired
                            <= 7 => "text-warning", // Expires within a week
                            <= 30 => "text-info", // Expires within a month
                            _ => "text-success" // Good
                        },
                        expiryLabel = m.DaysToExpiry switch
                        {
                            < 0 => "EXPIRED",
                            <= 7 => $"Expires in {m.DaysToExpiry} days",
                            <= 30 => $"Expires in {m.DaysToExpiry} days",
                            _ => $"Expires: {m.ExpiryDateFormatted}"
                        },
                        isNearExpiry = m.DaysToExpiry <= 30 && m.DaysToExpiry >= 0,
                        isExpired = m.DaysToExpiry < 0
                    })
                    .ToList();

                // Log successful data loading
                await _auditService.LogAsync("others_diagnosis", "PRESCDATA_OK", "system", null, null,
                    $"Prescription data loaded - Diseases: {diseases.Count}, Medicine Batches: {medicineStocks.Count}, Plant: {userPlantId}");

                return Json(new
                {
                    diseases = diseases.Select(d => new {
                        value = d.DiseaseId,
                        text = $"{d.DiseaseId} - {d.DiseaseName}",
                        description = d.DiseaseDesc ?? ""
                    }),
                    medicines = medicineDropdownItems,
                    plantInfo = new
                    {
                        plantId = userPlantId,
                        diseaseCount = diseases.Count,
                        medicineCount = medicineStocks.Count,
                        summary = new
                        {
                            totalMedicineBatches = medicineStocks.Count,
                            expiredBatches = medicineStocks.Count(m => m.DaysToExpiry < 0),
                            nearExpiryBatches = medicineStocks.Count(m => m.DaysToExpiry <= 30 && m.DaysToExpiry >= 0)
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription data with FIFO batch selection for Others Diagnosis");
                await _auditService.LogAsync("others_diagnosis", "PRESCDATA_FAIL", "system", null, null,
                    $"Prescription data loading failed: {ex.Message}");
                return Json(new
                {
                    diseases = new List<object>(),
                    medicines = new List<object>(),
                    message = "Error loading medicine data with FIFO batch selection"
                });
            }
        }
        // ======= NEW: Check available stock for a medicine batch WITH PLANT FILTERING =======
        [HttpGet]
        public async Task<IActionResult> CheckMedicineStock(int indentItemId)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log stock check attempt
                await _auditService.LogAsync("others_diagnosis", "CHECK_STOCK", indentItemId.ToString(), null, null,
                    $"Medicine stock check attempted for indent item: {indentItemId}, Plant: {userPlantId}");

                // NEW: Pass plant filtering to repository
                var availableStock = await _repository.GetAvailableStockAsync(indentItemId, userPlantId);

                // Log stock check result
                await _auditService.LogAsync("others_diagnosis", "STOCK_SUCCESS", indentItemId.ToString(), null, null,
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
                await _auditService.LogAsync("others_diagnosis", "STOCK_FAILED", indentItemId.ToString(), null, null,
                    $"Medicine stock check failed: {ex.Message}");
                return Json(new { success = false, availableStock = 0 });
            }
        }

        // ======= NEW: Validate prescription quantities against available stock WITH PLANT FILTERING =======
        [HttpPost]
        public async Task<IActionResult> ValidatePrescriptionStock(List<OthersPrescriptionMedicine> medicines)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log validation attempt
                await _auditService.LogAsync("others_diagnosis", "VALIDATE_STOCK", "bulk", null, null,
                    $"Prescription stock validation attempted for {medicines?.Count ?? 0} medicines, Plant: {userPlantId}");

                var validationResults = new List<OthersStockValidationResult>();

                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            // NEW: Pass plant filtering to repository
                            var availableStock = await _repository.GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);

                            var validationResult = new OthersStockValidationResult
                            {
                                IsValid = medicine.Quantity <= availableStock,
                                AvailableStock = availableStock,
                                RequestedQuantity = medicine.Quantity,
                                MedicineName = medicine.MedicineName,
                                BatchNo = medicine.BatchNo ?? "N/A"
                            };

                            if (!validationResult.IsValid)
                            {
                                validationResult.ErrorMessage = $"Insufficient stock for {medicine.MedicineName} (Batch: {medicine.BatchNo}). Available: {availableStock}, Requested: {medicine.Quantity}";
                            }

                            validationResults.Add(validationResult);
                        }
                    }
                }

                var allValid = validationResults.All(r => r.IsValid);
                var errorMessages = validationResults.Where(r => !r.IsValid).Select(r => r.ErrorMessage).ToList();

                // Log validation result
                var validationStatus = allValid ? "SUCCESS" : "FAILED";
                await _auditService.LogAsync("others_diagnosis", $"VALID_{validationStatus}", "bulk", null, null,
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
                await _auditService.LogAsync("others_diagnosis", "VALID_ERROR", "bulk", null, null,
                    $"Stock validation failed: {ex.Message}");
                return Json(new { success = false, isValid = false, errors = new[] { "Error validating stock availability" } });
            }
        }
        [HttpPost]
        public async Task<IActionResult> SaveDiagnosisAjax([FromBody] OthersDiagnosisViewModel model)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                if (model.PrescriptionMedicines?.Any() == true)
                {
                    _logger.LogInformation("=== Medicine Details ===");
                    foreach (var med in model.PrescriptionMedicines)
                    {
                        _logger.LogInformation("Medicine: MedItemId={MedItemId}, Quantity={Quantity}, Dose={Dose}, Name={Name}, IndentItemId={IndentItemId}, BatchNo={BatchNo}",
                            med.MedItemId, med.Quantity, med.Dose, med.MedicineName, med.IndentItemId, med.BatchNo);
                    }
                }

                // NEW: Validate plant assignment
                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("others_diagnosis", "AJAX_NO_PLANT", model.TreatmentId ?? "null", null, null,
                        "AJAX save denied - user has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                // Log AJAX save attempt (critical operation)
                await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_ATTEMPT", model.TreatmentId ?? "null", null, null,
                    $"AJAX diagnosis save attempted - TreatmentId: {model.TreatmentId}, Patient: {model.PatientName}, VisitType: {model.VisitType}, Diseases: {model.SelectedDiseaseIds?.Count ?? 0}, Medicines: {model.PrescriptionMedicines?.Count ?? 0}, Plant: {userPlantId}");

                // Check permissions for saving
                var userRole = await GetUserRoleAsync();
                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_DENIED", model.TreatmentId ?? "null", null, null,
                        $"AJAX diagnosis save denied - insufficient permissions for role: {userRole}");
                    return Json(new { success = false, message = "You don't have permission to save diagnoses." });
                }

                // UPDATED: TreatmentId validation and generation with error handling
                if (string.IsNullOrWhiteSpace(model.TreatmentId))
                {
                    try
                    {
                        model.TreatmentId = await _repository.GenerateNewTreatmentIdAsync();
                        _logger.LogInformation("Auto-generated TreatmentId: {TreatmentId}", model.TreatmentId);

                        await _auditService.LogAsync("others_diagnosis", "AJAX_TREATMENTID_GEN", model.TreatmentId, null, null,
                            $"TreatmentId auto-generated during AJAX save: {model.TreatmentId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-generate TreatmentId in AJAX save");
                        // Use timestamp-based fallback
                        model.TreatmentId = $"T{DateTime.Now:yyyyMMddHHmmss}";
                        _logger.LogInformation("Using fallback TreatmentId: {TreatmentId}", model.TreatmentId);

                        await _auditService.LogAsync("others_diagnosis", "AJAX_TREATMENTID_FALLBACK", model.TreatmentId, null, null,
                            $"TreatmentId generation failed in AJAX, using fallback: {model.TreatmentId}");
                    }
                }

                if (string.IsNullOrWhiteSpace(model.PatientName))
                {
                    await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_INVALID", model.TreatmentId, null, null,
                        "AJAX diagnosis save failed - Patient name is required");
                    return Json(new { success = false, message = "Patient name is required" });
                }

                // UPDATED: Age validation - now optional but if provided must be valid
                if (model.Age.HasValue && (model.Age.Value < 0 || model.Age.Value > 120))
                {
                    await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_INVALID", model.TreatmentId, null, null,
                        $"AJAX diagnosis save failed - Invalid age: {model.Age}");
                    return Json(new { success = false, message = "Age must be between 0 and 120 years" });
                }

                if (string.IsNullOrWhiteSpace(model.DiagnosedBy))
                {
                    model.DiagnosedBy = User.Identity?.Name + " - " + User.GetFullName() ?? "SYSTEM ADMIN";
                }

                // Default visit type if not provided
                if (string.IsNullOrWhiteSpace(model.VisitType))
                {
                    model.VisitType = "Regular Visitor";
                }
                if (model.PrescriptionMedicines?.Any() == true)
                {
                    foreach (var medicine in model.PrescriptionMedicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            // NEW: Pass plant filtering to repository
                            var availableStock = await _repository.GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);
                            if (medicine.Quantity > availableStock)
                            {
                                await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_NOSTOCK", model.TreatmentId, null, null,
                                    $"AJAX diagnosis save failed - insufficient stock for {medicine.MedicineName}");
                                return Json(new
                                {
                                    success = false,
                                    message = $"Insufficient stock for {medicine.MedicineName} (Batch: {medicine.BatchNo}). Available: {availableStock}, Requested: {medicine.Quantity}"
                                });
                            }
                        }
                    }
                }

                var createdBy = User.Identity?.Name + " - " + User.GetFullName() ?? "SYSTEM ADMIN";
                _logger.LogInformation("Calling SaveDiagnosisAsync with createdBy: {CreatedBy}", createdBy);

                // NEW: Pass plant filtering to repository
                var (success, errorMessage) = await _repository.SaveDiagnosisAsync(model, createdBy, userPlantId);

                _logger.LogInformation("SaveDiagnosisAsync result: Success={Success}, Error={Error}", success, errorMessage);

                if (success)
                {
                    // Log successful AJAX save (critical operation)
                    await _auditService.LogCreateAsync("others_diagnosis", model.TreatmentId ?? "unknown",
                        new { TreatmentId = model.TreatmentId, PatientName = model.PatientName, VisitType = model.VisitType, DiseasesCount = model.SelectedDiseaseIds?.Count ?? 0, MedicinesCount = model.PrescriptionMedicines?.Count ?? 0, PlantId = userPlantId },
                        $"AJAX diagnosis saved successfully - Patient: {model.PatientName}, CreatedBy: {createdBy}, Plant: {userPlantId}");

                    return Json(new { success = true, message = errorMessage });
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_FAILED", model.TreatmentId ?? "null", null, null,
                        $"AJAX diagnosis save failed: {errorMessage}");
                    return Json(new { success = false, message = errorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveDiagnosisAjax. Model: {@Model}", model);
                await _auditService.LogAsync("others_diagnosis", "AJAX_SAVE_ERROR", model.TreatmentId ?? "null", null, null,
                    $"AJAX diagnosis save failed with error: {ex.Message}");
                return Json(new { success = false, message = $"An error occurred while saving: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerateNewTreatmentId()
        {
            try
            {
                var newTreatmentId = await _repository.GenerateNewTreatmentIdAsync();
                _logger.LogInformation("Generated new Treatment ID via AJAX: {TreatmentId}", newTreatmentId);

                await _auditService.LogAsync("others_diagnosis", "TREATMENTID_AJAX", newTreatmentId, null, null,
                    $"TreatmentId generated via AJAX: {newTreatmentId}");

                return Json(new { success = true, treatmentId = newTreatmentId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating new treatment ID via AJAX");
                await _auditService.LogAsync("others_diagnosis", "TREATMENTID_AJAX_FAIL", "system", null, null,
                    $"TreatmentId AJAX generation failed: {ex.Message}");
                return Json(new { success = false, message = "Error generating treatment ID" });
            }
        }

        // GET: OthersDiagnosis/View/5
        public async Task<IActionResult> View(int id)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                _logger.LogInformation("Loading diagnosis view for ID: {DiagnosisId}, Plant: {PlantId}", id, userPlantId);

                // Log access to sensitive diagnosis details
                await _auditService.LogAsync("others_diagnosis", "VIEW_DETAILS", id.ToString(), null, null,
                    $"Others diagnosis details view accessed for ID: {id}, Plant: {userPlantId}");

                // NEW: Pass plant filtering to repository
                var diagnosis = await _repository.GetDiagnosisDetailsAsync(id, userPlantId);
                if (diagnosis == null)
                {
                    await _auditService.LogAsync("others_diagnosis", "VIEW_NOTFOUND", id.ToString(), null, null,
                        $"Others diagnosis not found for view access: {id} or access denied for plant: {userPlantId}");
                    TempData["Error"] = "Diagnosis record not found or access denied.";
                    return RedirectToAction(nameof(Index));
                }

                // Get user role and apply masking
                var userRole = await GetUserRoleAsync();
                ViewBag.UserRole = userRole;
                ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

                // Apply masking to sensitive data if user doesn't have appropriate role
                _maskingService.MaskObject(diagnosis, userRole);

                _logger.LogInformation("Loaded diagnosis with {MedicineCount} medicines and {DiseaseCount} diseases",
                    diagnosis.Medicines?.Count ?? 0, diagnosis.Diseases?.Count ?? 0);

                // Log successful access to sensitive diagnosis data
                await _auditService.LogViewAsync("others_diagnosis", id.ToString(),
                    $"Others diagnosis details viewed - Patient: {diagnosis.PatientName}, Role: {userRole}, Masked: {_maskingService.ShouldMaskData(userRole)}, Plant: {diagnosis.PlantName}");

                return View(diagnosis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading diagnosis for ID: {DiagnosisId}", id);
                await _auditService.LogAsync("others_diagnosis", "VIEW_ERROR", id.ToString(), null, null,
                    $"Others diagnosis view failed with error: {ex.Message}");
                TempData["Error"] = "Error loading diagnosis details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ======= APPROVAL ENDPOINTS WITH PLANT FILTERING =======

        [HttpGet]
        public async Task<IActionResult> GetPendingApprovalCount()
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log pending approval count access
                await _auditService.LogAsync("others_diagnosis", "GET_PENDING", "system", null, null,
                    $"Pending approval count access attempted, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("others_diagnosis", "PENDING_DENIED", "system", null, null,
                        $"Pending approval count access denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can view pending approvals." });
                }

                // NEW: Pass plant filtering to repository
                var count = await _repository.GetPendingApprovalCountAsync(userPlantId);

                // Log successful access
                await _auditService.LogAsync("others_diagnosis", "PENDING_OK", "system", null, null,
                    $"Pending approval count accessed - Count: {count}, Plant: {userPlantId}");

                return Json(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approval count");
                await _auditService.LogAsync("others_diagnosis", "PENDING_FAIL", "system", null, null,
                    $"Get pending approval count failed: {ex.Message}");
                return Json(new { success = false, count = 0 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PendingApprovals()
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log pending approvals access
                await _auditService.LogAsync("others_diagnosis", "PEND_APPR_VIEW", "system", null, null,
                    $"Pending approvals access attempted, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("others_diagnosis", "APPR_DENIED", "system", null, null,
                        $"Pending approvals access denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can view pending approvals." });
                }

                // NEW: Pass plant filtering to repository
                var pendingApprovals = await _repository.GetPendingApprovalsAsync(userPlantId);

                // Apply masking for non-doctors (though this shouldn't happen as we check above)
                foreach (var approval in pendingApprovals)
                {
                    _maskingService.MaskObject(approval, userRole);
                }

                ViewBag.UserRole = userRole;

                // Log successful access
                await _auditService.LogAsync("others_diagnosis", "APPR_VIEW_OK", "system", null, null,
                    $"Pending approvals accessed - Count: {pendingApprovals.Count()}, Plant: {userPlantId}");

                return PartialView("_PendingApprovalsModal", pendingApprovals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending approvals");
                await _auditService.LogAsync("others_diagnosis", "APPR_VIEW_FAIL", "system", null, null,
                    $"Pending approvals access failed: {ex.Message}");
                return Json(new { success = false, message = "Error loading pending approvals." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveDiagnosis(int diagnosisId)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log diagnosis approval attempt (critical operation)
                await _auditService.LogAsync("others_diagnosis", "APPROVE_ATTEMPT", diagnosisId.ToString(), null, null,
                    $"Others diagnosis approval attempted for ID: {diagnosisId}, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("others_diagnosis", "APPROVE_DENIED", diagnosisId.ToString(), null, null,
                        $"Others diagnosis approval denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve diagnoses." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "unknown";
                // NEW: Pass plant filtering to repository
                var success = await _repository.ApproveDiagnosisAsync(diagnosisId, approvedBy, userPlantId);

                if (success)
                {
                    // Log successful approval (critical operation)
                    await _auditService.LogUpdateAsync("others_diagnosis", diagnosisId.ToString(),
                        null, new { Status = "Approved", ApprovedBy = approvedBy, PlantId = userPlantId },
                        $"Others diagnosis approved successfully by: {approvedBy}, Plant: {userPlantId}");

                    return Json(new { success = true, message = "Diagnosis approved successfully." });
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "APPROVE_FAILED", diagnosisId.ToString(), null, null,
                        "Others diagnosis approval failed - not found or already processed or access denied");
                    return Json(new { success = false, message = "Diagnosis not found, already processed, or access denied." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving diagnosis {diagnosisId}");
                await _auditService.LogAsync("others_diagnosis", "APPROVE_ERROR", diagnosisId.ToString(), null, null,
                    $"Others diagnosis approval failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error approving diagnosis." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectDiagnosis(int diagnosisId, string rejectionReason)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log diagnosis rejection attempt (critical operation)
                await _auditService.LogAsync("others_diagnosis", "REJECT_ATTEMPT", diagnosisId.ToString(), null, null,
                    $"Others diagnosis rejection attempted for ID: {diagnosisId}, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("others_diagnosis", "REJECT_DENIED", diagnosisId.ToString(), null, null,
                        $"Others diagnosis rejection denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can reject diagnoses." });
                }

                if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Length < 10)
                {
                    await _auditService.LogAsync("others_diagnosis", "REJECT_INVALID", diagnosisId.ToString(), null, null,
                        "Others diagnosis rejection validation failed - insufficient rejection reason");
                    return Json(new { success = false, message = "Please provide a detailed rejection reason (minimum 10 characters)." });
                }

                var rejectedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "unknown";
                // NEW: Pass plant filtering to repository
                var success = await _repository.RejectDiagnosisAsync(diagnosisId, rejectionReason, rejectedBy, userPlantId);

                if (success)
                {
                    // Log successful rejection (critical operation)
                    await _auditService.LogUpdateAsync("others_diagnosis", diagnosisId.ToString(),
                        null, new { Status = "Rejected", RejectedBy = rejectedBy, RejectionReason = rejectionReason, PlantId = userPlantId },
                        $"Others diagnosis rejected by: {rejectedBy}, Reason: {rejectionReason}, Plant: {userPlantId}");

                    return Json(new { success = true, message = "Diagnosis rejected successfully." });
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "REJECT_NOTFOUND", diagnosisId.ToString(), null, null,
                        "Others diagnosis rejection failed - not found or already processed or access denied");
                    return Json(new { success = false, message = "Diagnosis not found, already processed, or access denied." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting diagnosis {diagnosisId}");
                await _auditService.LogAsync("others_diagnosis", "REJECT_ERROR", diagnosisId.ToString(), null, null,
                    $"Others diagnosis rejection failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error rejecting diagnosis." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAllDiagnoses(List<int> diagnosisIds)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log bulk approval attempt (critical operation)
                await _auditService.LogAsync("others_diagnosis", "BULK_APPROVE", "bulk", null, null,
                    $"Bulk others diagnosis approval attempted for {diagnosisIds?.Count ?? 0} diagnoses, Plant: {userPlantId}");

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("others_diagnosis", "BULK_DENIED", "bulk", null, null,
                        $"Bulk others diagnosis approval denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve diagnoses." });
                }

                if (diagnosisIds == null || !diagnosisIds.Any())
                {
                    await _auditService.LogAsync("others_diagnosis", "BULK_INVALID", "bulk", null, null,
                        "Bulk others diagnosis approval validation failed - no diagnoses selected");
                    return Json(new { success = false, message = "No diagnoses selected for approval." });
                }

                var approvedBy = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "unknown";
                // NEW: Pass plant filtering to repository
                var approvedCount = await _repository.ApproveAllDiagnosesAsync(diagnosisIds, approvedBy, userPlantId);

                if (approvedCount > 0)
                {
                    // Log successful bulk approval (critical operation)
                    await _auditService.LogUpdateAsync("others_diagnosis", "bulk",
                        null, new { ApprovedCount = approvedCount, ApprovedBy = approvedBy, DiagnosisIds = diagnosisIds, PlantId = userPlantId },
                        $"Bulk others diagnosis approval successful - {approvedCount} diagnoses approved by: {approvedBy}, Plant: {userPlantId}");

                    return Json(new { success = true, message = $"{approvedCount} diagnosis(es) approved successfully." });
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "BULK_NONE", "bulk", null, null,
                        "Bulk others diagnosis approval failed - no diagnoses were approved");
                    return Json(new { success = false, message = "No diagnoses were approved. They may have been already processed or access denied." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving multiple diagnoses");
                await _auditService.LogAsync("others_diagnosis", "BULK_ERROR", "bulk", null, null,
                    $"Bulk others diagnosis approval failed with error: {ex.Message}");
                return Json(new { success = false, message = "Error approving diagnoses." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RawMedicineQuery(int diagnosisId)
        {
            try
            {
                // NEW: Get user's plant ID
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("others_diagnosis", "RAW_QUERY", diagnosisId.ToString(), null, null,
                    $"Raw medicine query accessed for diagnosis: {diagnosisId}, Plant: {userPlantId}");

                // NEW: Pass plant filtering to repository
                var result = await _repository.GetRawMedicineDataAsync(diagnosisId, userPlantId);
                return Json(result);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("others_diagnosis", "RAW_QUERY_ERROR", diagnosisId.ToString(), null, null,
                    $"Raw medicine query failed: {ex.Message}");
                return Json(new { error = ex.Message });
            }
        }

        // ======= NEW: Helper method to get current user's plant ID =======
        private async Task<int?> GetCurrentUserPlantIdAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                return await _repository.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("others_diagnosis", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        // Helper method to get user role
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
                    .FirstOrDefaultAsync(u => u.full_name == userName || u.email == userName || u.adid == userName);

                return user?.SysRole?.role_name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role");
                await _auditService.LogAsync("others_diagnosis", "ROLE_ERROR", "system", null, null,
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

                await _auditService.LogAsync("others_diagnosis", "EDIT_ATTEMPT", id.ToString(), null, null,
                    $"Others diagnosis edit form access attempted for ID: {id}, Plant: {userPlantId}");

                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_DENIED", id.ToString(), null, null,
                        $"Others diagnosis edit denied - insufficient permissions for role: {userRole}");
                    TempData["Error"] = "You don't have permission to edit diagnoses.";
                    return RedirectToAction(nameof(Index));
                }

                var permissionResult = await _repository.CanEditDiagnosisAsync(id, userPlantId);
                if (!permissionResult.CanEdit)
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_NOTALLOWED", id.ToString(), null, null,
                        $"Others diagnosis edit not allowed: {permissionResult.Message}");
                    TempData["Error"] = permissionResult.Message;
                    return RedirectToAction(nameof(Index));
                }

                var editModel = await _repository.GetDiagnosisForEditAsync(id, userPlantId);
                if (editModel == null)
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_NOTFOUND", id.ToString(), null, null,
                        $"Others diagnosis not found for edit in plant: {userPlantId}");
                    TempData["Error"] = "Diagnosis not found or cannot be edited.";
                    return RedirectToAction(nameof(Index));
                }

                // NEW: Check if user is creator or doctor
                var isCreator = editModel.DiagnosedBy == currentUser;
                var isDoctor = userRole?.ToLower() == "doctor";

                if (!isCreator && !isDoctor)
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_UNAUTHORIZED", id.ToString(), null, null,
                        $"User {currentUser} not authorized to edit diagnosis created by {editModel.DiagnosedBy}");
                    TempData["Error"] = "You can only edit your own diagnoses or if you're a doctor.";
                    return RedirectToAction(nameof(Index));
                }

                _maskingService.MaskObject(editModel, userRole);

                await _auditService.LogViewAsync("others_diagnosis", id.ToString(),
                    $"Others diagnosis edit form accessed - Patient: {editModel.PatientName}, Status: {editModel.ApprovalStatus}, Plant: {userPlantId}");

                return View(editModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit form for Others diagnosis {id}");
                await _auditService.LogAsync("others_diagnosis", "EDIT_ERROR", id.ToString(), null, null,
                    $"Others diagnosis edit form load failed: {ex.Message}");
                TempData["Error"] = "Error loading diagnosis for editing.";
                return RedirectToAction(nameof(Index));
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> Edit(int id)
        //{
        //    try
        //    {
        //        var userPlantId = await GetCurrentUserPlantIdAsync();
        //        var userRole = await GetUserRoleAsync();

        //        ViewBag.UserRole = userRole;
        //        ViewBag.UserPlantId = userPlantId;
        //        ViewBag.ShouldMaskData = _maskingService.ShouldMaskData(userRole);

        //        // Log edit attempt
        //        await _auditService.LogAsync("others_diagnosis", "EDIT_ATTEMPT", id.ToString(), null, null,
        //            $"Others diagnosis edit form access attempted for ID: {id}, Plant: {userPlantId}");

        //        // Check if user has permission to edit diagnoses
        //        if (_maskingService.ShouldMaskData(userRole))
        //        {
        //            await _auditService.LogAsync("others_diagnosis", "EDIT_DENIED", id.ToString(), null, null,
        //                $"Others diagnosis edit denied - insufficient permissions for role: {userRole}");
        //            TempData["Error"] = "You don't have permission to edit diagnoses.";
        //            return RedirectToAction(nameof(Index));
        //        }

        //        // Check if diagnosis can be edited
        //        var permissionResult = await _repository.CanEditDiagnosisAsync(id, userPlantId);
        //        if (!permissionResult.CanEdit)
        //        {
        //            await _auditService.LogAsync("others_diagnosis", "EDIT_NOTALLOWED", id.ToString(), null, null,
        //                $"Others diagnosis edit not allowed: {permissionResult.Message}");
        //            TempData["Error"] = permissionResult.Message;
        //            return RedirectToAction(nameof(Index));
        //        }

        //        // Get diagnosis for editing
        //        var editModel = await _repository.GetDiagnosisForEditAsync(id, userPlantId);
        //        if (editModel == null)
        //        {
        //            await _auditService.LogAsync("others_diagnosis", "EDIT_NOTFOUND", id.ToString(), null, null,
        //                $"Others diagnosis not found for edit in plant: {userPlantId}");
        //            TempData["Error"] = "Diagnosis not found or cannot be edited.";
        //            return RedirectToAction(nameof(Index));
        //        }

        //        // Apply data masking if needed
        //        _maskingService.MaskObject(editModel, userRole);

        //        // Log successful access
        //        await _auditService.LogViewAsync("others_diagnosis", id.ToString(),
        //            $"Others diagnosis edit form accessed - Patient: {editModel.PatientName}, Status: {editModel.ApprovalStatus}, Plant: {userPlantId}");

        //        return View(editModel);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error loading edit form for Others diagnosis {id}");
        //        await _auditService.LogAsync("others_diagnosis", "EDIT_ERROR", id.ToString(), null, null,
        //            $"Others diagnosis edit form load failed: {ex.Message}");
        //        TempData["Error"] = "Error loading diagnosis for editing.";
        //        return RedirectToAction(nameof(Index));
        //    }
        //}

        [HttpPost]
        public async Task<IActionResult> Edit(int diagnosisId, List<int> selectedDiseases,
            List<OthersPrescriptionMedicine> medicines, OthersDiagnosisViewModel basicInfo)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetUserRoleAsync();

                // Log edit save attempt
                await _auditService.LogAsync("others_diagnosis", "EDIT_SAVE_ATTEMPT", diagnosisId.ToString(), null, null,
                    $"Others diagnosis edit save attempted for ID: {diagnosisId}, Plant: {userPlantId}");

                // Check permissions
                if (_maskingService.ShouldMaskData(userRole))
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_SAVE_DENIED", diagnosisId.ToString(), null, null,
                        $"Others diagnosis edit save denied for role: {userRole}");
                    return Json(new { success = false, message = "You don't have permission to edit diagnoses." });
                }

                // Check if can still edit
                var permissionResult = await _repository.CanEditDiagnosisAsync(diagnosisId, userPlantId);
                if (!permissionResult.CanEdit)
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_SAVE_NOTALLOWED", diagnosisId.ToString(), null, null,
                        $"Others diagnosis edit save not allowed: {permissionResult.Message}");
                    return Json(new { success = false, message = permissionResult.Message });
                }

                // Validate input
                if (selectedDiseases?.Any() != true)
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_SAVE_NODISEASE", diagnosisId.ToString(), null, null,
                        "Others diagnosis edit save failed - No diseases selected");
                    return Json(new { success = false, message = "Please select at least one disease." });
                }

                var userId = User.FindFirst("user_id")?.Value ?? User.Identity?.Name + " - " + User.GetFullName() ?? "anonymous";

                // Update diagnosis
                var updateResult = await _repository.UpdateDiagnosisAsync(
                    diagnosisId, selectedDiseases, medicines ?? new List<OthersPrescriptionMedicine>(),
                    basicInfo, userId, userPlantId);

                if (updateResult.Success)
                {
                    // Log successful update
                    await _auditService.LogUpdateAsync("others_diagnosis", diagnosisId.ToString(),
                        null, new
                        {
                            DiseasesCount = selectedDiseases.Count,
                            MedicinesCount = medicines?.Count ?? 0,
                            ModifiedBy = userId,
                            PlantId = userPlantId,
                            StockAdjusted = updateResult.StockAdjusted,
                            AffectedMedicines = updateResult.AffectedMedicines
                        },
                        $"Others diagnosis updated successfully by: {userId} in plant: {userPlantId}");

                    return Json(new
                    {
                        success = true,
                        message = updateResult.Message,
                        redirectUrl = Url.Action("Index")
                    });
                }
                else
                {
                    await _auditService.LogAsync("others_diagnosis", "EDIT_SAVE_FAILED", diagnosisId.ToString(), null, null,
                        $"Others diagnosis edit save failed: {updateResult.Message}");

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
                _logger.LogError(ex, $"Error updating Others diagnosis {diagnosisId}");
                await _auditService.LogAsync("others_diagnosis", "EDIT_SAVE_ERROR", diagnosisId.ToString(), null, null,
                    $"Others diagnosis edit save error: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "Error updating diagnosis: " + ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDiagnosisEditData(int diagnosisId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log access to edit data
                await _auditService.LogAsync("others_diagnosis", "GET_EDIT_DATA", diagnosisId.ToString(), null, null,
                    $"Others diagnosis edit data access attempted for ID: {diagnosisId}, Plant: {userPlantId}");

                // Check permissions
                var permissionResult = await _repository.CanEditDiagnosisAsync(diagnosisId, userPlantId);
                if (!permissionResult.CanEdit)
                {
                    return Json(new { success = false, message = permissionResult.Message });
                }

                // Get diagnosis data
                var editModel = await _repository.GetDiagnosisForEditAsync(diagnosisId, userPlantId);
                if (editModel == null)
                {
                    return Json(new { success = false, message = "Diagnosis not found." });
                }

                // Get fresh prescription data for editing
                var prescriptionData = await GetDiagnosisPrescriptionDataForEdit(userPlantId);

                // Log successful access
                await _auditService.LogAsync("others_diagnosis", "EDIT_DATA_OK", diagnosisId.ToString(), null, null,
                    $"Others diagnosis edit data loaded successfully for plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    diagnosis = new
                    {
                        diagnosisId = editModel.DiagnosisId,
                        treatmentId = editModel.TreatmentId,
                        patientName = editModel.PatientName,
                        selectedDiseaseIds = editModel.SelectedDiseaseIds,
                        bloodPressure = editModel.BloodPressure,
                        pulseRate = editModel.PulseRate,
                        sugar = editModel.Sugar,
                        remarks = editModel.Remarks,
                        lastVisitDate = editModel.LastVisitDate?.ToString("yyyy-MM-dd"),
                        currentMedicines = await GetCurrentMedicinesWithStockInfoAsync(editModel.CurrentMedicines, userPlantId, editModel.ApprovalStatus)
                    },
                    diseases = prescriptionData.diseases,
                    medicines = prescriptionData.medicines
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Others diagnosis edit data for {diagnosisId}");
                await _auditService.LogAsync("others_diagnosis", "EDIT_DATA_FAIL", diagnosisId.ToString(), null, null,
                    $"Others diagnosis get edit data failed: {ex.Message}");

                return Json(new { success = false, message = "Error loading diagnosis data." });
            }
        }

        private async Task<dynamic> GetDiagnosisPrescriptionDataForEdit(int? userPlantId)
        {
            // Get plant-wise diseases
            var diseases = await _repository.GetDiseasesAsync(userPlantId);

            // Get plant-wise medicines with batch information
            var medicineStocks = await _repository.GetMedicinesFromCompounderIndentAsync(userPlantId);

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
                    text = $"{m.MedItemId} - {m.BaseName} - {m.MedItemName} | Batch: {m.BatchNo}",
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
            List<OthersMedicineEdit> currentMedicines, int? userPlantId, string approvalStatus)
        {
            var result = new List<dynamic>();

            if (currentMedicines?.Any() != true)
                return result;

            try
            {
                _logger.LogInformation($"Getting stock info for {currentMedicines.Count} existing Others medicines in plant {userPlantId}");

                // Get all available medicine stocks for the plant
                var medicineStocks = await _repository.GetMedicinesFromCompounderIndentAsync(userPlantId);

                _logger.LogInformation($"Found {medicineStocks.Count} total medicine stock records for Others diagnosis");

                foreach (var medicine in currentMedicines)
                {
                    _logger.LogInformation($"Looking for stock info for Others medicine ID: {medicine.MedItemId}, Name: {medicine.MedicineName}");

                    // Find ALL matching stock records for this medicine
                    var allMatchingStocks = medicineStocks
                        .Where(stock => stock.MedItemId == medicine.MedItemId)
                        .ToList();

                    _logger.LogInformation($"Found {allMatchingStocks.Count} stock records for Others medicine ID {medicine.MedItemId}");

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

                        _logger.LogWarning($"No available stock for Others medicine ID {medicine.MedItemId}, using first stock record for info");
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

                        _logger.LogInformation($"Others medicine ID {medicine.MedItemId}: Stock {matchingStock.AvailableStock} + Current {medicine.Quantity} = Display {displayStock}");

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
                        _logger.LogWarning($"No stock records found for Others medicine ID {medicine.MedItemId} - {medicine.MedicineName}");

                        // Return basic info without stock data
                        result.Add(new
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
                            expiryDateFormatted = "No stock record",
                            daysToExpiry = -999,
                            expiryClass = "text-warning",
                            expiryLabel = "No current stock"
                        });
                    }
                }

                _logger.LogInformation($"Completed stock info lookup for Others diagnosis - {result.Count} medicines processed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock info for existing Others medicines");

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
    }
}