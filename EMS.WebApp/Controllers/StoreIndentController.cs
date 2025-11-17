using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessStoreIndent")]
    public class StoreIndentController : Controller
    {
        private readonly IStoreIndentRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public StoreIndentController(IStoreIndentRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Log index access for security monitoring
                var userRole = await GetUserRoleAsync();
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("store_indent", "INDEX_VIEW", "main", null, null,
                    $"Store indent module accessed by user role: {userRole}, Plant: {userPlantId}");

                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "INDEX_FAILED", "main", null, null,
                    $"Failed to load store indent index: {ex.Message}");
                throw;
            }
        }

        public async Task<IActionResult> LoadData(string indentType = null)
        {
            try
            {
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log data access attempt
                await _auditService.LogAsync("store_indent", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - IndentType: {indentType ?? "all"}, User: {currentUser}, Plant: {userPlantId}");

                // Get the list with user filtering for drafts and plant filtering
                IEnumerable<StoreIndent> list;

                if (string.IsNullOrEmpty(indentType))
                {
                    list = await _repo.ListAsync(currentUser, userPlantId);
                }
                else if (indentType == "Store Inventory")
                {
                    // For Store Inventory, show all approved indents from user's plant
                    list = await _repo.ListByStatusAsync("Approved", currentUser, userPlantId);
                }
                else
                {
                    list = await _repo.ListByTypeAsync(indentType, currentUser, userPlantId);
                }

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";

                var result = new List<object>();

                foreach (var x in list)
                {
                    // Get items with their pending quantities to determine button visibility
                    var items = await _repo.GetItemsByIndentIdAsync(x.IndentId, userPlantId);
                    var allItemsReceived = items.Any() && items.All(item => item.PendingQuantity == 0);
                    var hasItems = items.Any();
                    var hasPendingItems = items.Any(item => item.PendingQuantity > 0);

                    var itemData = new
                    {
                        x.IndentId,
                        IndentNo = x.IndentId.ToString(),
                        IndentType = indentType == "Store Inventory" ? "Store Inventory" : x.IndentType,
                        IndentDate = x.IndentDate.ToString("dd/MM/yyyy"),
                        x.Status,
                        x.CreatedBy,
                        CreatedDate = x.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        PlantName = x.OrgPlant?.plant_name ?? "Unknown Plant", // NEW: Show plant info
                        // IMPORTANT: Review & Approve button only shows for doctors with pending indents
                        CanApproveReject = isDoctor && x.Status == "Pending" && x.IndentType != "Draft Indent" && indentType != "Store Inventory",
                        // For regular items, show edit based on normal rules
                        //CanEdit = indentType == "Store Inventory" ?
                        //    (!isDoctor && hasItems) : // Show edit for Store Inventory if has items (handles both pending and fully received)
                        //    (!isDoctor && ((x.IndentType == "Draft Indent" && x.CreatedBy == currentUser) || // Only creator can edit drafts
                        //     (x.IndentType != "Draft Indent" && (x.Status == "Pending" || string.IsNullOrEmpty(x.Status))))), // Others can edit if pending

                        CanEdit = indentType == "Store Inventory" ?
                            (!isDoctor && hasItems) : // Show edit for Store Inventory if has items (handles both pending and fully received)
                            (!isDoctor && ((x.IndentType == "Draft Indent" && x.CreatedBy == currentUser) || // Only creator can edit drafts
                             (x.IndentType != "Draft Indent" && (x.Status == "Pending" || string.IsNullOrEmpty(x.Status))))) || // Others can edit if pending
                            (isDoctor && indentType == "Approved Indents"), // NEW: Doctors can edit when viewing "Approved Indents" filter



                        CanDelete = indentType != "Store Inventory" && !isDoctor && ((x.IndentType == "Draft Indent" && x.CreatedBy == currentUser) || // Only creator can delete drafts
                                   (x.Status == "Pending" || string.IsNullOrEmpty(x.Status))), // Others can delete if pending
                        IsDoctor = isDoctor, // Pass doctor status to frontend
                        AllItemsReceived = allItemsReceived,
                        HasItems = hasItems,
                        HasPendingItems = hasPendingItems
                    };

                    result.Add(itemData);
                }

                // Log successful data access
                await _auditService.LogAsync("store_indent", "LOAD_SUCCESS", "multiple", null, null,
                    $"Data loaded successfully - Count: {result.Count}, IndentType: {indentType ?? "all"}, Plant: {userPlantId}");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("store_indent", "LOAD_FAILED", "multiple", null, null,
                    $"Failed to load data: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        // Action for approve/reject modal with plant-wise access check
        public async Task<IActionResult> ApproveReject(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log approval/rejection access attempt
                await _auditService.LogAsync("store_indent", "APPROVE_VIEW", id.ToString(), null, null,
                    $"Approve/reject modal accessed for indent: {id}, Plant: {userPlantId}");

                var item = await _repo.GetByIdWithItemsAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("store_indent", "APPROVE_NOTFND", id.ToString(), null, null,
                        $"Indent not found for approve/reject: {id} or access denied for plant: {userPlantId}");
                    return NotFound();
                }

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("store_indent", "APPROVE_DENIED", id.ToString(), null, null,
                        $"Approve/reject access denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
                }

                // Check if status is pending
                if (item.Status != "Pending")
                {
                    await _auditService.LogAsync("store_indent", "APPROVE_INVALID", id.ToString(), null, null,
                        $"Invalid status for approval: {item.Status}");
                    return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
                }

                // Populate medicine dropdown for editing
                await PopulateMedicineDropdownAsync();

                // Log successful access
                await _auditService.LogAsync("store_indent", "APPROVE_ACCESS", id.ToString(), null, null,
                    $"Approve/reject modal accessed successfully - Status: {item.Status}, Plant: {item.OrgPlant?.plant_name}");

                return PartialView("_ApproveRejectModal", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "APPROVE_ERROR", id.ToString(), null, null,
                    $"Error accessing approve/reject modal: {ex.Message}");
                throw;
            }
        }

        // Action to update status with comprehensive audit logging
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int indentId, string status, string comments)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log update status attempt (critical operation)
                await _auditService.LogAsync("store_indent", "UPDATE_STATUS", indentId.ToString(), null, null,
                    $"Status update attempted - Status: {status}, IndentId: {indentId}, Plant: {userPlantId}");

                // Sanitize input before processing
                comments = SanitizeString(comments);
                status = SanitizeString(status);

                // Additional security validation
                if (!IsCommentsSecure(comments))
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_SECURITY", indentId.ToString(), null, null,
                        "Status update blocked - insecure comments detected");
                    return Json(new { success = false, message = "Invalid input detected in comments. Please remove any script tags or unsafe characters." });
                }

                // Validate input
                if (string.IsNullOrWhiteSpace(comments) || comments.Length < 2)
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_INVALID", indentId.ToString(), null, null,
                        "Status update validation failed - insufficient comments");
                    return Json(new { success = false, message = "Please provide detailed comments (minimum 2 characters)." });
                }

                if (comments.Length > 500)
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_TOOLONG", indentId.ToString(), null, null,
                        "Status update validation failed - comments too long");
                    return Json(new { success = false, message = "Comments cannot exceed 500 characters." });
                }

                if (status != "Approved" && status != "Rejected")
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_BADSTAT", indentId.ToString(), null, null,
                        $"Status update validation failed - invalid status: {status}");
                    return Json(new { success = false, message = "Invalid status." });
                }

                // Check if user has doctor role
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_DENIED", indentId.ToString(), null, null,
                        $"Status update denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
                }

                // Get the indent with plant filtering
                var indent = await _repo.GetByIdAsync(indentId, userPlantId);
                if (indent == null)
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_NOTFND", indentId.ToString(), null, null,
                        "Status update failed - indent not found or access denied");
                    return Json(new { success = false, message = "Store Indent not found or access denied." });
                }

                // Check if status is pending
                if (indent.Status != "Pending")
                {
                    await _auditService.LogAsync("store_indent", "UPDATE_WRONGST", indentId.ToString(), null, null,
                        $"Status update failed - current status: {indent.Status}");
                    return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
                }

                // Store old values for audit
                var oldIndent = new { Status = indent.Status, IndentType = indent.IndentType, Comments = indent.Comments };

                // Update the indent
                indent.Status = status;
                indent.Comments = comments;
                indent.ApprovedBy = User.Identity?.Name + " - " + User.GetFullName();
                indent.ApprovedDate = DateTime.Now;

                // Also update IndentType to match the new status
                indent.IndentType = status == "Approved" ? "Approved Indents" : "Rejected Indents";

                await _repo.UpdateAsync(indent);

                // Log successful status update (critical operation)
                await _auditService.LogAsync("store_indent", "UPDATE_SUCCESS", indentId.ToString(),
                    oldIndent, new { Status = status, IndentType = indent.IndentType, Comments = comments },
                    $"Indent {status.ToLower()} successfully by: {User.Identity?.Name}");

                return Json(new { success = true, message = $"Store Indent {status.ToLower()} successfully." });
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                await _auditService.LogAsync("store_indent", "UPDATE_ERROR", indentId.ToString(), null, null,
                    $"Status update failed with error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the status." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log create form access
                await _auditService.LogAsync("store_indent", "CREATE_FORM", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync(); // New method for indent types

                var model = new StoreIndent
                {
                    IndentDate = DateTime.Today,
                    IndentType = "Pending Indents", // Default to pending
                    Status = "Pending",
                    PlantId = (short)(userPlantId ?? 1) // NEW: Auto-populate user's plant
                };

                // Log successful form access
                await _auditService.LogAsync("store_indent", "CREATE_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "CREATE_ERROR", "new", null, null,
                    $"Create form error: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(StoreIndent model, string medicinesJson, string actionType = "submit")
        {
            string recordId = "new";

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.IndentId.ToString();

                // NEW: Enhanced plant ID handling
                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("store_indent", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    await PopulateMedicineDropdownAsync();
                    await PopulateIndentTypeDropdownAsync();
                    return PartialView("_CreateEdit", model);
                }

                // Ensure plant ID is set correctly
                model.PlantId = (short)userPlantId.Value;

                // Log create attempt (critical operation)
                await _auditService.LogAsync("store_indent", "CREATE_ATTEMPT", recordId, null, model,
                    $"Indent creation attempted - ActionType: {actionType}, IndentType: {model.IndentType}, Plant: {model.PlantId}");

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync();

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    await _auditService.LogAsync("store_indent", "CREATE_SECURITY", recordId, null, model,
                        "Create blocked - insecure input detected");
                    ViewBag.Error = "Invalid input detected. Please remove any script tags or unsafe characters.";
                    return PartialView("_CreateEdit", model);
                }

                // Set indent type based on action
                if (actionType.ToLower() == "save")
                {
                    model.IndentType = "Draft Indent";
                    model.Status = "Draft";
                }
                else
                {
                    // For submit, if it was a draft, change to pending
                    if (model.IndentType == "Draft Indent")
                    {
                        model.IndentType = "Pending Indents";
                        model.Status = "Pending";
                    }
                    else
                    {
                        // Set status based on indent type for other types
                        SetStatusBasedOnIndentType(model);
                    }
                }

                // IMPORTANT: Remove PlantId from ModelState validation temporarily for debugging
                ModelState.Remove("PlantId");

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    System.Diagnostics.Debug.WriteLine($"DEBUG - Validation Errors: {validationErrors}");

                    // Log specific validation errors
                    foreach (var error in ModelState)
                    {
                        if (error.Value.Errors.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"DEBUG - Field: {error.Key}, Errors: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                        }
                    }

                    await _auditService.LogAsync("store_indent", "CREATE_INVALID", recordId, null, model,
                        $"Create validation failed: {validationErrors}");

                    ViewBag.Error = $"Please check the form for validation errors: {validationErrors}";
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_storeindent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("store_indent", "CREATE_RATELIMIT", recordId, null, model,
                        $"Create rate limited - {timestamps.Count} attempts in 5 minutes");

                    return Json(new { success = false, message = "⚠ You can only create 5 Store Indents every 5 minutes. Please wait and try again." });
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                model.CreatedBy = User.Identity?.Name + " - " + User.GetFullName();
                model.CreatedDate = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"DEBUG - About to save model with PlantId: {model.PlantId}");

                // Save to database
                await _repo.AddAsync(model);

                recordId = model.IndentId.ToString();
                System.Diagnostics.Debug.WriteLine($"DEBUG - Saved successfully with IndentId: {model.IndentId}");

                // Process medicines if provided
                var medicineValidationResult = await ProcessMedicinesForCreate(model.IndentId, medicinesJson, userPlantId);
                if (!medicineValidationResult.Success)
                {
                    await _auditService.LogAsync("store_indent", "CREATE_MED_FAIL", recordId, null, model,
                        $"Medicine processing failed: {medicineValidationResult.ErrorMessage}");
                    ViewBag.Error = medicineValidationResult.ErrorMessage;
                    return PartialView("_CreateEdit", model);
                }

                // Log successful creation (critical operation)
                await _auditService.LogAsync("store_indent", "CREATE_SUCCESS", recordId, null, model,
                    $"Indent created successfully - ActionType: {actionType}, HasMedicines: {medicineValidationResult.HasMedicines}, Plant: {model.PlantId}");

                if (actionType.ToLower() == "save")
                {
                    // For save, return JSON response instead of keeping form open
                    if (medicineValidationResult.HasMedicines)
                    {
                        return Json(new { success = true, message = "Store Indent saved as draft successfully with medicines!", indentId = model.IndentId, actionType = "save" });
                    }
                    else
                    {
                        return Json(new { success = true, message = "Store Indent saved as draft successfully! You can continue editing or submit when ready.", indentId = model.IndentId, actionType = "save" });
                    }
                }
                else
                {
                    // For submit, close the modal
                    if (medicineValidationResult.HasMedicines)
                    {
                        ViewBag.Success = "Store Indent submitted successfully with medicines!";
                    }
                    else
                    {
                        ViewBag.Success = "Store Indent submitted successfully! You can now add medicines.";
                    }

                    return Json(new { success = true, redirectToEdit = !medicineValidationResult.HasMedicines, indentId = model.IndentId });
                }
            }
            catch (Exception ex)
            {
                // Enhanced error logging with full details
                var detailedError = $"Create failed: {ex.Message}";
                if (ex.InnerException != null)
                {
                    detailedError += $" Inner: {ex.InnerException.Message}";
                }

                System.Diagnostics.Debug.WriteLine($"DEBUG - Full Exception: {ex}");
                System.Diagnostics.Debug.WriteLine($"DEBUG - Stack Trace: {ex.StackTrace}");

                // Log creation failure with detailed error
                await _auditService.LogAsync("store_indent", "CREATE_FAILED", recordId, null, model,
                    detailedError);

                // Handle specific database errors
                if (ex.InnerException?.Message.Contains("FOREIGN KEY constraint") == true)
                {
                    ViewBag.Error = "Plant assignment error. Please contact administrator.";
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
                    ViewBag.Error = $"An error occurred while creating the indent. Details: {ex.Message}";
                }

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync();
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id, string filterType = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log edit form access attempt
                await _auditService.LogAsync("store_indent", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed for plant: {userPlantId}, FilterType: {filterType}");

                var item = await _repo.GetByIdWithItemsAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("store_indent", "EDIT_NOTFOUND", id.ToString(), null, null,
                        $"Edit attempted on non-existent indent or unauthorized access for plant: {userPlantId}");
                    return NotFound();
                }

                // Security check: Doctors can only edit approved indents
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() == "doctor" && item.Status != "Approved")
                {
                    await _auditService.LogAsync("store_indent", "EDIT_DOC_DENY", id.ToString(), null, null,
                        "Edit denied - doctors can only edit approved indents");
                    return Json(new { success = false, message = "Access denied. Doctors can only edit approved indents." });
                }

                // Security check: Only allow editing drafts by their creators
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("store_indent", "EDIT_DRAFT_DENY", id.ToString(), null, null,
                        $"Draft edit denied - not creator: {currentUser} vs {item.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only edit your own drafts." });
                }

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync(item.CreatedBy);

                // NEW: Pass filter type to view
                ViewBag.FilterType = filterType;

                // Log successful edit form access
                await _auditService.LogAsync("store_indent", "EDIT_FORM_OK", id.ToString(), null, null,
                    $"Edit form accessed successfully - Status: {item.Status}, Type: {item.IndentType}, Plant: {item.OrgPlant?.plant_name}, FilterType: {filterType}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "EDIT_FORM_ERR", id.ToString(), null, null,
                    $"Edit form error: {ex.Message}");
                throw;
            }
        }
        [HttpPost]
        public async Task<IActionResult> Edit(StoreIndent model, string medicinesJson, string actionType = "submit")
        {
            var recordId = model.IndentId.ToString();
            StoreIndent oldIndent = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get existing entity for audit comparison
                oldIndent = await _repo.GetByIdAsync(model.IndentId, userPlantId);
                if (oldIndent == null)
                {
                    await _auditService.LogAsync("store_indent", "EDIT_NOTFOUND", recordId, null, model,
                        "Edit attempted on non-existent indent or unauthorized access");
                    return NotFound();
                }

                // Ensure plant ID matches user's plant (security check)
                model.PlantId = oldIndent.PlantId;

                // Log edit attempt (critical operation)
                await _auditService.LogAsync("store_indent", "EDIT_ATTEMPT", recordId, oldIndent, model,
                    $"Indent edit attempted - ActionType: {actionType}, Plant: {model.PlantId}");

                // Security check: Doctors cannot edit indents - they can only review

                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() == "doctor" && oldIndent.Status != "Approved")
                {
                    await _auditService.LogAsync("store_indent", "EDIT_DOC_DENY", recordId, oldIndent, model,
                        "Edit denied - doctors can only edit approved indents");
                    return Json(new { success = false, message = "Access denied. Doctors can only edit approved indents." });
                }

                //var userRole = await GetUserRoleAsync();
                //if (userRole?.ToLower() == "doctor")
                //{
                //    await _auditService.LogAsync("store_indent", "EDIT_DOC_DENY", recordId, oldIndent, model,
                //        "Edit denied - doctors can only review");
                //    return Json(new { success = false, message = "Access denied. Doctors can only review indents, not edit them." });
                //}

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync(model.CreatedBy);

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    await _auditService.LogAsync("store_indent", "EDIT_SECURITY", recordId, oldIndent, model,
                        "Edit blocked - insecure input detected");
                    ViewBag.Error = "Invalid input detected. Please remove any script tags or unsafe characters.";
                    return PartialView("_CreateEdit", model);
                }

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("store_indent", "EDIT_INVALID", recordId, oldIndent, model,
                        $"Edit validation failed: {validationErrors}");

                    ViewBag.Error = "Please check the form for validation errors.";
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic for edits
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_storeindent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("store_indent", "EDIT_RATELIMIT", recordId, oldIndent, model,
                        $"Edit rate limited - {timestamps.Count} attempts in 5 minutes");

                    return Json(new { success = false, message = "⚠ You can only edit 10 Store Indents every 5 minutes. Please wait and try again." });
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Get existing entity to preserve creation info
                var existingIndent = await _repo.GetByIdAsync(model.IndentId, userPlantId);
                if (existingIndent == null)
                {
                    await _auditService.LogAsync("store_indent", "EDIT_GONE", recordId, oldIndent, model,
                        "Edit failed - indent no longer exists");
                    ViewBag.Error = "Store Indent not found.";
                    return PartialView("_CreateEdit", model);
                }

                // Handle draft to pending transition
                if (actionType.ToLower() == "submit" && existingIndent.IndentType == "Draft Indent")
                {
                    model.IndentType = "Pending Indents";
                    model.Status = "Pending";
                }
                else if (actionType.ToLower() == "save")
                {
                    // Keep as draft if saving
                    if (existingIndent.IndentType == "Draft Indent")
                    {
                        model.IndentType = "Draft Indent";
                        model.Status = "Draft";
                    }
                }
                else
                {
                    // Set status based on indent type for other cases
                    SetStatusBasedOnIndentType(model);
                }

                // Update the existing tracked entity with new values
                existingIndent.IndentType = model.IndentType;
                existingIndent.IndentDate = model.IndentDate;
                existingIndent.Status = model.Status;
                existingIndent.Comments = model.Comments; // Include comments in update
                // Keep original CreatedBy, CreatedDate, and PlantId (don't update these)

                await _repo.UpdateAsync(existingIndent);

                // Process medicines
                var medicineValidationResult = await ProcessMedicinesForEdit(model.IndentId, medicinesJson, userPlantId);
                if (!medicineValidationResult.Success)
                {
                    await _auditService.LogAsync("store_indent", "EDIT_MED_FAIL", recordId, oldIndent, model,
                        $"Medicine processing failed: {medicineValidationResult.ErrorMessage}");
                    ViewBag.Error = medicineValidationResult.ErrorMessage;
                    return PartialView("_CreateEdit", model);
                }

                // Log successful edit (critical operation)
                await _auditService.LogAsync("store_indent", "EDIT_SUCCESS", recordId, oldIndent, existingIndent,
                    $"Indent updated successfully - ActionType: {actionType}, Plant: {existingIndent.PlantId}");

                if (actionType.ToLower() == "save")
                {
                    // For save, return JSON response to keep modal open
                    return Json(new { success = true, message = "Store Indent saved successfully! You can continue editing or submit when ready.", actionType = "save" });
                }
                else
                {
                    // For submit, close the modal
                    ViewBag.Success = "Store Indent updated successfully!";
                    return Json(new { success = true });
                }
            }
            catch (Exception ex)
            {
                // Log edit failure
                await _auditService.LogAsync("store_indent", "EDIT_FAILED", recordId, oldIndent, model,
                    $"Indent edit failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A constraint violation occurred. Please check your input.";
                }
                else
                {
                    ViewBag.Error = $"An error occurred while updating the indent: {ex.Message}";
                }
                return PartialView("_CreateEdit", model);
            }
        }

        // Add individual medicine item with audit logging and plant filtering
        [HttpPost]
        public async Task<IActionResult> AddMedicineItem(int indentId, int medItemId, string vendorCode, int raisedQuantity, int receivedQuantity = 0)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log medicine add attempt
                await _auditService.LogAsync("store_indent", "ADD_MEDICINE", indentId.ToString(), null, null,
                    $"Medicine add attempted - MedItemId: {medItemId}, VendorCode: {vendorCode}, RaisedQty: {raisedQuantity}, Plant: {userPlantId}");

                // Sanitize vendor code input (allow empty since it's optional)
                vendorCode = SanitizeString(vendorCode ?? "");

                // Vendor code is optional - only validate format if provided
                if (!string.IsNullOrWhiteSpace(vendorCode) && !IsVendorCodeSecure(vendorCode))
                {
                    await _auditService.LogAsync("store_indent", "ADD_MED_INSECURE", indentId.ToString(), null, null,
                        $"Medicine add blocked - insecure vendor code: {vendorCode}");
                    return Json(new { success = false, message = "Invalid vendor code format. Please use only letters, numbers, hyphens, and underscores." });
                }

                // Get the indent to check permissions and plant access
                var indent = await _repo.GetByIdAsync(indentId, userPlantId);
                if (indent == null)
                {
                    await _auditService.LogAsync("store_indent", "ADD_MED_NOTFND", indentId.ToString(), null, null,
                        "Medicine add failed - indent not found or access denied");
                    return Json(new { success = false, message = "Store Indent not found or access denied." });
                }

                // Security check
                var userRole = await GetUserRoleAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                if (indent.Status != "Pending" && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("store_indent", "ADD_MED_STATUS", indentId.ToString(), null, null,
                        $"Medicine add denied - wrong status: {indent.Status}");
                    return Json(new { success = false, message = "Only pending indents can be modified, or doctors can modify during approval." });
                }

                // Check for duplicates - ONLY check for same medicine, NOT vendor code
                if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medItemId, null, userPlantId))
                {
                    await _auditService.LogAsync("store_indent", "ADD_MED_DUP", indentId.ToString(), null, null,
                        $"Medicine add failed - duplicate: {medItemId}");
                    return Json(new { success = false, message = "This medicine is already added to this indent." });
                }

                // Create new medicine item
                var newItem = new StoreIndentItem
                {
                    IndentId = indentId,
                    MedItemId = medItemId,
                    VendorCode = string.IsNullOrWhiteSpace(vendorCode) ? null : vendorCode, // Set to null if empty
                    RaisedQuantity = raisedQuantity,
                    ReceivedQuantity = receivedQuantity
                };

                await _repo.AddItemAsync(newItem);
                // Log successful medicine addition
                await _auditService.LogAsync("store_indent", "ADD_MED_OK", newItem.IndentItemId.ToString(),
                    null, newItem, $"Medicine item added successfully - Medicine: {medItemId}, Plant: {indent.PlantId}");

                return Json(new
                {
                    success = true,
                    message = "Medicine item added successfully!",
                    itemId = newItem.IndentItemId
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "ADD_MED_FAIL", indentId.ToString(), null, null,
                    $"Medicine add failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while adding the medicine item." });
            }
        }

        // Update individual medicine item with audit logging and plant filtering
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineItem(int indentItemId, int? medItemId = null, string vendorCode = null, int? raisedQuantity = null, int? receivedQuantity = null, decimal? unitPrice = null, string batchNo = null, DateTime? expiryDate = null, string remark = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log medicine update attempt
                await _auditService.LogAsync("store_indent", "UPD_MEDICINE", indentItemId.ToString(), null, null,
                    $"Medicine update attempted - ItemId: {indentItemId}, Plant: {userPlantId}");

                // Sanitize string inputs
                if (vendorCode != null)
                {
                    vendorCode = SanitizeString(vendorCode);
                    // Only validate vendor code if not empty (vendor code is optional)
                    if (!string.IsNullOrWhiteSpace(vendorCode) && !IsVendorCodeSecure(vendorCode))
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_INSEC", indentItemId.ToString(), null, null,
                            $"Medicine update blocked - insecure vendor code: {vendorCode}");
                        return Json(new { success = false, message = "Invalid vendor code format. Please remove any unsafe characters." });
                    }
                }

                if (batchNo != null)
                {
                    batchNo = SanitizeString(batchNo);
                    if (!IsBatchNoSecure(batchNo))
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_BATCH", indentItemId.ToString(), null, null,
                            $"Medicine update blocked - insecure batch: {batchNo}");
                        return Json(new { success = false, message = "Invalid batch number format. Please remove any unsafe characters." });
                    }
                }

                // Sanitize remark
                if (remark != null)
                {
                    remark = SanitizeString(remark);
                    if (!IsCommentsSecure(remark))
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_REMARK", indentItemId.ToString(), null, null,
                            $"Medicine update blocked - insecure remark: {remark}");
                        return Json(new { success = false, message = "Invalid remark format. Please remove any unsafe characters." });
                    }
                }

                // Validate expiry date
                if (expiryDate.HasValue)
                {
                    var expiryValidation = ValidateExpiryDate(expiryDate, "Expiry date");
                    if (!expiryValidation.IsValid)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_EXPIRY", indentItemId.ToString(), null, null,
                            $"Medicine update blocked - invalid expiry: {expiryValidation.ErrorMessage}");
                        return Json(new { success = false, message = expiryValidation.ErrorMessage });
                    }
                }

                // Get existing item with plant filtering
                var existingItem = await _repo.GetItemByIdAsync(indentItemId, userPlantId);
                if (existingItem == null)
                {
                    await _auditService.LogAsync("store_indent", "UPD_MED_NOTFND", indentItemId.ToString(), null, null,
                        "Medicine update failed - item not found or access denied");
                    return Json(new { success = false, message = "Medicine item not found or access denied." });
                }
                // NEW: Check if item can be edited (received quantity must be 0)
                if (existingItem.ReceivedQuantity > 0)
                {
                    await _auditService.LogAsync("store_indent", "UPD_MED_LOCKED", indentItemId.ToString(), existingItem, null,
                        $"Medicine update blocked - item already received (qty: {existingItem.ReceivedQuantity})");
                    return Json(new { success = false, message = "Cannot edit medicine item that has already been received. Received quantity must be 0 to allow editing." });
                }
                // Store old values for audit
                var oldItem = new
                {
                    existingItem.MedItemId,
                    existingItem.VendorCode,
                    existingItem.RaisedQuantity,
                    existingItem.ReceivedQuantity,
                    existingItem.UnitPrice,
                    existingItem.BatchNo,
                    existingItem.ExpiryDate,
                    existingItem.Remark
                };

                // Update fields if provided
                if (medItemId.HasValue)
                    existingItem.MedItemId = medItemId.Value;

                if (vendorCode != null) // Allow setting to empty string (optional field)
                    existingItem.VendorCode = string.IsNullOrWhiteSpace(vendorCode) ? null : vendorCode;

                if (raisedQuantity.HasValue)
                {
                    if (raisedQuantity.Value <= 0)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_QTYINV", indentItemId.ToString(), existingItem, null,
                            "Medicine update blocked - invalid raised quantity");
                        return Json(new { success = false, message = "Raised quantity must be greater than 0." });
                    }
                    existingItem.RaisedQuantity = raisedQuantity.Value;
                }

                if (receivedQuantity.HasValue)
                {
                    if (receivedQuantity.Value < 0)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_RCVNEG", indentItemId.ToString(), existingItem, null,
                            "Medicine update blocked - negative received quantity");
                        return Json(new { success = false, message = "Received quantity cannot be negative." });
                    }

                    if (receivedQuantity.Value > existingItem.RaisedQuantity)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_MED_EXCEED", indentItemId.ToString(), existingItem, null,
                            "Medicine update blocked - received exceeds raised");
                        return Json(new { success = false, message = "Received quantity cannot exceed raised quantity." });
                    }

                    existingItem.ReceivedQuantity = receivedQuantity.Value;
                }

                if (unitPrice.HasValue)
                {
                    existingItem.UnitPrice = unitPrice;
                    existingItem.TotalAmount = unitPrice.Value * existingItem.ReceivedQuantity;
                }

                // Update remark (optional field)
                if (remark != null) // Allow empty string to clear the value
                {
                    existingItem.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
                }

                // Update batch no and expiry date (for Store Inventory)
                if (batchNo != null) // Allow empty string to clear the value
                {
                    existingItem.BatchNo = string.IsNullOrWhiteSpace(batchNo) ? null : batchNo.Trim();
                }

                if (expiryDate.HasValue)
                {
                    existingItem.ExpiryDate = expiryDate.Value;
                }
                else if (Request.Form.ContainsKey("expiryDate"))
                {
                    // If expiryDate parameter exists but is null/empty, clear the field
                    existingItem.ExpiryDate = null;
                }

                await _repo.UpdateItemAsync(existingItem);

                // Log successful medicine update
                await _auditService.LogAsync("store_indent", "UPD_MED_OK", indentItemId.ToString(), oldItem, existingItem,
                    $"Medicine item updated successfully - Plant: {existingItem.StoreIndent?.PlantId}");

                // Return updated values for UI refresh
                return Json(new
                {
                    success = true,
                    message = "Medicine item updated successfully!",
                    data = new
                    {
                        receivedQuantity = existingItem.ReceivedQuantity,
                        pendingQuantity = existingItem.PendingQuantity,
                        unitPrice = existingItem.UnitPrice,
                        totalAmount = existingItem.TotalAmount,
                        batchNo = existingItem.BatchNo,
                        expiryDate = existingItem.ExpiryDate?.ToString("yyyy-MM-dd"),
                        remark = existingItem.Remark
                    }
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "UPD_MED_FAIL", indentItemId.ToString(), null, null,
                    $"Medicine update failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the medicine item." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineItemWithReason(int indentItemId, int? receivedQuantity = null, decimal? unitPrice = null, string batchNo = null, DateTime? expiryDate = null, string remark = null, string editReason = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log medicine update with reason attempt
                await _auditService.LogAsync("store_indent", "UPD_MED_REASON", indentItemId.ToString(), null, null,
                    $"Medicine update with reason attempted - ItemId: {indentItemId}, Plant: {userPlantId}");

                // Sanitize inputs
                batchNo = SanitizeString(batchNo);
                remark = SanitizeString(remark);
                editReason = SanitizeString(editReason);

                // Security validation
                if (!string.IsNullOrEmpty(batchNo) && !IsBatchNoSecure(batchNo))
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_BATCH", indentItemId.ToString(), null, null,
                        $"Medicine update with reason blocked - insecure batch: {batchNo}");
                    return Json(new { success = false, message = "Invalid batch number format. Please remove any unsafe characters." });
                }

                if (!IsCommentsSecure(editReason))
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_COMM", indentItemId.ToString(), null, null,
                        "Medicine update with reason blocked - insecure reason");
                    return Json(new { success = false, message = "Invalid characters in edit reason. Please remove any script tags or unsafe characters." });
                }

                if (!string.IsNullOrEmpty(remark) && !IsCommentsSecure(remark))
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_REMARK", indentItemId.ToString(), null, null,
                        "Medicine update with reason blocked - insecure remark");
                    return Json(new { success = false, message = "Invalid characters in remark. Please remove any script tags or unsafe characters." });
                }

                // Validate expiry date
                if (expiryDate.HasValue)
                {
                    var expiryValidation = ValidateExpiryDate(expiryDate, "Expiry date");
                    if (!expiryValidation.IsValid)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_REAS_EXP", indentItemId.ToString(), null, null,
                            $"Medicine update with reason blocked - invalid expiry: {expiryValidation.ErrorMessage}");
                        return Json(new { success = false, message = expiryValidation.ErrorMessage });
                    }
                }

                // Validate reason
                if (string.IsNullOrWhiteSpace(editReason) || editReason.Length < 10)
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_SHORT", indentItemId.ToString(), null, null,
                        "Medicine update with reason failed - insufficient reason");
                    return Json(new { success = false, message = "Please provide a detailed reason for editing (minimum 10 characters)." });
                }

                if (editReason.Length > 500)
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_LONG", indentItemId.ToString(), null, null,
                        "Medicine update with reason failed - reason too long");
                    return Json(new { success = false, message = "Edit reason cannot exceed 500 characters." });
                }

                // Get existing item with plant filtering
                var existingItem = await _repo.GetItemByIdAsync(indentItemId, userPlantId);
                if (existingItem == null)
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_NOTFD", indentItemId.ToString(), null, null,
                        "Medicine update with reason failed - item not found or access denied");
                    return Json(new { success = false, message = "Medicine item not found or access denied." });
                }

                // Get the indent to check permissions with plant filtering
                var indent = await _repo.GetByIdAsync(existingItem.IndentId, userPlantId);
                if (indent == null)
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_INDENT", existingItem.IndentId.ToString(), existingItem, null,
                        "Medicine update with reason failed - indent not found or access denied");
                    return Json(new { success = false, message = "Store Indent not found or access denied." });
                }

                // Allow editing for any approved indent
                if (indent.Status != "Approved")
                {
                    await _auditService.LogAsync("store_indent", "UPD_REAS_MODE", indentItemId.ToString(), existingItem, null,
                        "Medicine update with reason denied - not approved status");
                    return Json(new { success = false, message = "Only approved items can be edited with reason." });
                }

                // Store original values for audit trail
                var originalItem = new
                {
                    ReceivedQuantity = existingItem.ReceivedQuantity,
                    UnitPrice = existingItem.UnitPrice,
                    BatchNo = existingItem.BatchNo,
                    ExpiryDate = existingItem.ExpiryDate,
                    Remark = existingItem.Remark
                };

                // Update fields if provided
                if (receivedQuantity.HasValue)
                {
                    if (receivedQuantity.Value < 0)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_REAS_RCVNEG", indentItemId.ToString(), existingItem, null,
                            "Medicine update with reason blocked - negative received quantity");
                        return Json(new { success = false, message = "Received quantity cannot be negative." });
                    }

                    if (receivedQuantity.Value > existingItem.RaisedQuantity)
                    {
                        await _auditService.LogAsync("store_indent", "UPD_REAS_EXCEED", indentItemId.ToString(), existingItem, null,
                            "Medicine update with reason blocked - received exceeds raised");
                        return Json(new { success = false, message = "Received quantity cannot exceed raised quantity." });
                    }

                    existingItem.ReceivedQuantity = receivedQuantity.Value;
                }

                if (unitPrice.HasValue)
                {
                    existingItem.UnitPrice = unitPrice;
                    existingItem.TotalAmount = unitPrice.Value * existingItem.ReceivedQuantity;
                }

                // Update remark
                if (remark != null) // Allow empty string to clear the value
                {
                    existingItem.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
                }

                // Update batch no and expiry date
                if (batchNo != null) // Allow empty string to clear the value
                {
                    existingItem.BatchNo = string.IsNullOrWhiteSpace(batchNo) ? null : batchNo.Trim();
                }

                if (expiryDate.HasValue)
                {
                    existingItem.ExpiryDate = expiryDate.Value;
                }
                else if (Request.Form.ContainsKey("expiryDate"))
                {
                    // If expiryDate parameter exists but is null/empty, clear the field
                    existingItem.ExpiryDate = null;
                }

                await _repo.UpdateItemAsync(existingItem);

                // Log the edit with reason (critical operation)
                await _auditService.LogAsync("store_indent", "UPD_REAS_OK", indentItemId.ToString(),
                    originalItem, existingItem, $"Medicine item updated with reason: {editReason}, Plant: {indent.PlantId}");

                // Return updated values for UI refresh
                return Json(new
                {
                    success = true,
                    message = "Medicine item updated successfully with reason logged!",
                    data = new
                    {
                        receivedQuantity = existingItem.ReceivedQuantity,
                        pendingQuantity = existingItem.PendingQuantity,
                        unitPrice = existingItem.UnitPrice,
                        totalAmount = existingItem.TotalAmount,
                        batchNo = existingItem.BatchNo,
                        expiryDate = existingItem.ExpiryDate?.ToString("yyyy-MM-dd"),
                        remark = existingItem.Remark
                    }
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "UPD_REAS_FAIL", indentItemId.ToString(), null, null,
                    $"Medicine update with reason failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the medicine item." });
            }
        }
        // Delete individual medicine item with audit logging and plant filtering
        [HttpPost]
        public async Task<IActionResult> DeleteMedicineItem(int indentItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log medicine delete attempt
                await _auditService.LogAsync("store_indent", "DEL_MEDICINE", indentItemId.ToString(), null, null,
                    $"Medicine delete attempted - ItemId: {indentItemId}, Plant: {userPlantId}");

                // Get existing item to check permissions with plant filtering
                var existingItem = await _repo.GetItemByIdAsync(indentItemId, userPlantId);
                if (existingItem == null)
                {
                    await _auditService.LogAsync("store_indent", "DEL_MED_NOTFND", indentItemId.ToString(), null, null,
                        "Medicine delete failed - item not found or access denied");
                    return Json(new { success = false, message = "Medicine item not found or access denied." });
                }
                // NEW: Check if item can be deleted (received quantity must be 0)
                if (existingItem.ReceivedQuantity > 0)
                {
                    await _auditService.LogAsync("store_indent", "DEL_MED_LOCKED", indentItemId.ToString(), existingItem, null,
                        $"Medicine delete blocked - item already received (qty: {existingItem.ReceivedQuantity})");
                    return Json(new { success = false, message = "Cannot delete medicine item that has already been received. Received quantity must be 0 to allow deletion." });
                }
                // Get the indent to check status and permissions with plant filtering
                var indent = await _repo.GetByIdAsync(existingItem.IndentId, userPlantId);
                if (indent == null)
                {
                    await _auditService.LogAsync("store_indent", "DEL_MED_INDENT", existingItem.IndentId.ToString(), existingItem, null,
                        "Medicine delete failed - indent not found or access denied");
                    return Json(new { success = false, message = "Store Indent not found or access denied." });
                }

                // Security check: Only allow deleting from pending indents or by doctors during approval
                var userRole = await GetUserRoleAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                if (indent.Status != "Pending" && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("store_indent", "DEL_MED_STATUS", indentItemId.ToString(), existingItem, null,
                        $"Medicine delete denied - wrong status: {indent.Status}");
                    return Json(new { success = false, message = "Only pending indents can be modified, or doctors can modify during approval." });
                }

                // Additional check for draft indents - only creator can delete
                if (indent.IndentType == "Draft Indent" && indent.CreatedBy != currentUser && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("store_indent", "DEL_MED_OWNER", indentItemId.ToString(), existingItem, null,
                        $"Medicine delete denied - not owner: {currentUser} vs {indent.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only delete items from your own drafts." });
                }

                // Delete the item
                await _repo.DeleteItemAsync(indentItemId, userPlantId);

                // Log successful medicine deletion
                await _auditService.LogAsync("store_indent", "DEL_MED_OK", indentItemId.ToString(),
                    existingItem, null, $"Medicine item deleted successfully - Plant: {indent.PlantId}");

                return Json(new { success = true, message = "Medicine item deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "DEL_MED_FAIL", indentItemId.ToString(), null, null,
                    $"Medicine delete failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the medicine item." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            StoreIndent itemToDelete = null;

            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Get entity before deletion for audit with plant filtering
                itemToDelete = await _repo.GetByIdAsync(id, userPlantId);
                if (itemToDelete == null)
                {
                    await _auditService.LogAsync("store_indent", "DELETE_NOTFND", id.ToString(), null, null,
                        $"Delete attempted on non-existent indent: {id} or access denied for plant: {userPlantId}");
                    return Json(new { success = false, message = "Store Indent not found or access denied." });
                }

                // Log delete attempt
                await _auditService.LogAsync("store_indent", "DELETE_ATTEMPT", id.ToString(), itemToDelete, null,
                    $"Indent deletion attempted - Type: {itemToDelete.IndentType}, Status: {itemToDelete.Status}, Plant: {itemToDelete.PlantId}");

                // Security check: Doctors cannot delete indents - they can only review
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() == "doctor")
                {
                    await _auditService.LogAsync("store_indent", "DELETE_DOC_DEN", id.ToString(), itemToDelete, null,
                        "Delete denied - doctors cannot delete indents");
                    return Json(new { success = false, message = "Access denied. Doctors can only review indents, not delete them." });
                }

                // Security check: Only allow deleting drafts by their creators
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                if (itemToDelete.IndentType == "Draft Indent" && itemToDelete.CreatedBy != currentUser)
                {
                    await _auditService.LogAsync("store_indent", "DELETE_OWNER", id.ToString(), itemToDelete, null,
                        $"Delete denied - not owner: {currentUser} vs {itemToDelete.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only delete your own drafts." });
                }

                // Additional check: Don't allow deleting approved/rejected indents
                if (itemToDelete.Status == "Approved" || itemToDelete.Status == "Rejected")
                {
                    await _auditService.LogAsync("store_indent", "DELETE_STATUS", id.ToString(), itemToDelete, null,
                        $"Delete denied - status: {itemToDelete.Status}");
                    return Json(new { success = false, message = "Cannot delete approved or rejected indents." });
                }

                await _repo.DeleteAsync(id, userPlantId);

                // Log successful deletion
                await _auditService.LogAsync("store_indent", "DELETE_SUCCESS", id.ToString(),
                    itemToDelete, null, $"Indent deleted successfully - Type: {itemToDelete.IndentType}, Plant: {itemToDelete.PlantId}");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log delete failure
                await _auditService.LogAsync("store_indent", "DELETE_FAILED", id.ToString(), itemToDelete, null,
                    $"Indent deletion failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the indent." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log details view attempt
                await _auditService.LogAsync("store_indent", "DETAILS_VIEW", id.ToString(), null, null,
                    $"Details view attempted for plant: {userPlantId}");

                var item = await _repo.GetByIdWithItemsAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("store_indent", "DETAILS_NOTFND", id.ToString(), null, null,
                        $"Details view failed - indent not found or access denied for plant: {userPlantId}");
                    return NotFound();
                }

                // Security check: Only allow viewing drafts by their creators
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser)
                {
                    await _auditService.LogAsync("store_indent", "DETAILS_OWNER", id.ToString(), item, null,
                        $"Details view denied - not owner: {currentUser} vs {item.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only view your own drafts." });
                }

                // Log successful details view
                await _auditService.LogAsync("store_indent", "DETAILS_OK", id.ToString(), null, null,
                    $"Details viewed successfully - Type: {item.IndentType}, Status: {item.Status}, Plant: {item.OrgPlant?.plant_name}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "DETAILS_ERROR", id.ToString(), null, null,
                    $"Details view error: {ex.Message}");
                throw;
            }
        }

        // AJAX methods for medicine management with audit logging and plant filtering
        public async Task<IActionResult> GetMedicineDetails(int medItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log medicine details access
                await _auditService.LogAsync("store_indent", "MED_DETAILS", medItemId.ToString(), null, null,
                    $"Medicine details requested: {medItemId}, Plant: {userPlantId}");

                // Pass userPlantId for plant-wise filtering
                var medicine = await _repo.GetMedicineByIdAsync(medItemId, userPlantId);
                if (medicine == null)
                {
                    await _auditService.LogAsync("store_indent", "MED_DET_NOTFND", medItemId.ToString(), null, null,
                        "Medicine details not found");
                    return Json(new { success = false, message = "Medicine not found" });
                }

                // Log successful access
                await _auditService.LogAsync("store_indent", "MED_DET_OK", medItemId.ToString(), null, null,
                    $"Medicine details accessed: {medicine.MedItemName}");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        medItemId = medicine.MedItemId,
                        medItemName = medicine.MedItemName,
                        companyName = medicine.CompanyName ?? "Not Defined",
                        reorderLimit = medicine.ReorderLimit
                    }
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "MED_DET_ERROR", medItemId.ToString(), null, null,
                    $"Medicine details error: {ex.Message}");
                return Json(new { success = false, message = "Error loading medicine details" });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetMedicineItems(int indentId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log medicine items access
                await _auditService.LogAsync("store_indent", "GET_MED_ITEMS", indentId.ToString(), null, null,
                    $"Medicine items requested for indent: {indentId}, Plant: {userPlantId}");

                var items = await _repo.GetItemsByIndentIdAsync(indentId, userPlantId);
                var result = items.Select((item, index) => new {
                    indentItemId = item.IndentItemId,
                    slNo = index + 1,
                    medItemId = item.MedItemId,
                    medItemName = item.MedMaster?.MedItemName,
                    companyName = item.MedMaster?.CompanyName ?? "Not Defined",
                    vendorCode = item.VendorCode,
                    raisedQuantity = item.RaisedQuantity,  // Updated property name
                    receivedQuantity = item.ReceivedQuantity,  // New property
                    pendingQuantity = item.PendingQuantity,  // Calculated property
                    unitPrice = item.UnitPrice,
                    totalAmount = item.TotalAmount,
                    remark = item.Remark,
                    batchNo = item.BatchNo,
                    expiryDate = item.ExpiryDate?.ToString("yyyy-MM-dd")
                });

                // Log successful access
                await _auditService.LogAsync("store_indent", "GET_ITEMS_OK", indentId.ToString(), null, null,
                    $"Medicine items loaded - Count: {result.Count()}, Plant: {userPlantId}");

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "GET_ITEMS_ERR", indentId.ToString(), null, null,
                    $"Medicine items error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading medicine items." });
            }
        }

        // Get all batches for a medicine with audit logging and plant filtering
        [HttpGet]
        public async Task<IActionResult> GetBatchesForMedicine(int indentItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log batch access
                await _auditService.LogAsync("store_indent", "GET_BATCHES", indentItemId.ToString(), null, null,
                    $"Batches requested for medicine item: {indentItemId}, Plant: {userPlantId}");

                var batches = await _repo.GetBatchesByIndentItemIdAsync(indentItemId, userPlantId);

                // Log successful access
                await _auditService.LogAsync("store_indent", "GET_BATCH_OK", indentItemId.ToString(), null, null,
                    $"Batches loaded - Count: {batches.Count()}, Plant: {userPlantId}");

                return Json(new { success = true, data = batches });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "GET_BATCH_ERR", indentItemId.ToString(), null, null,
                    $"Get batches error: {ex.Message}");
                return Json(new { success = false, data = new List<object>() });
            }
        }

        // Save all batches (called from modal) - Enhanced with expiry date validation, audit logging, and plant filtering
        // Enhanced Save all batches method with Clear All Batches support
        [HttpPost]
        public async Task<IActionResult> SaveBatchesForMedicine([FromBody] List<StoreIndentBatchDto> batches)
        {
            try
            {



                int indentItemId1 = 0;

                if (Request.Headers.TryGetValue("X-IndentItemId", out var headerValue))
                {
                    _ = int.TryParse(headerValue.FirstOrDefault(), out indentItemId1);
                }

                if (indentItemId1 == 0 && Request.Query.TryGetValue("indentItemId", out var qsValue))
                {
                    _ = int.TryParse(qsValue.FirstOrDefault(), out indentItemId1);
                }


                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Allow null or empty batches for clearing functionality
                if (batches == null)
                {
                    batches = new List<StoreIndentBatchDto>();
                }

                int indentItemId = 0;
                if (batches.Count > 0)
                {
                    indentItemId = batches[0].IndentItemId;
                }
                else
                {
                    // For clear all batches, we need to get the indentItemId from the request
                    var indentItemIdString = Request.Headers["X-IndentItemId"].FirstOrDefault();
                    if (string.IsNullOrEmpty(indentItemIdString) || !int.TryParse(indentItemIdString, out indentItemId))
                    {
                        return Json(new { success = false, message = "Indent item ID is required for batch operations." });
                    }
                }

                // Log batch save attempt
                await _auditService.LogAsync("store_indent", "SAVE_BATCHES", indentItemId.ToString(), null, null,
                    $"Save batches attempted for medicine item: {indentItemId}, Count: {batches.Count}, Plant: {userPlantId}");

                // Get the indent item to check the requested quantity with plant filtering
                var indentItem = await _repo.GetItemByIdAsync(indentItemId, userPlantId);
                if (indentItem == null)
                {
                    await _auditService.LogAsync("store_indent", "SAVE_BATCH_NF", indentItemId.ToString(), null, null,
                        "Save batches failed - indent item not found or access denied");
                    return Json(new { success = false, message = "Indent item not found or access denied." });
                }

                var requestedQuantity = indentItem.RaisedQuantity;

                // If clearing all batches (empty array), confirm this is intentional
                if (batches.Count == 0)
                {
                    // Log clear all batches action
                    await _auditService.LogAsync("store_indent", "CLEAR_BATCHES", indentItemId.ToString(), null, null,
                        $"Clear all batches requested for medicine item: {indentItemId}, Plant: {userPlantId}");

                    // Use the existing AddOrUpdateBatchesAsync method with empty list
                    var emptyBatchEntities = new List<StoreIndentBatch>();
                    await _repo.AddOrUpdateBatchesAsync(indentItemId, emptyBatchEntities, userPlantId);

                    // Log successful batch clearing
                    await _auditService.LogAsync("store_indent", "CLEAR_BATCH_OK", indentItemId.ToString(), null, null,
                        $"All batches cleared successfully for medicine item: {indentItemId}, Plant: {userPlantId}");

                    return Json(new
                    {
                        success = true,
                        message = "All batches have been cleared successfully!",
                        totalReceived = 0,
                        totalAvailable = 0,
                        totalBatches = 0,
                        requestedQuantity = requestedQuantity
                    });
                }

                // Validate all expiry dates in batches
                var batchValidation = ValidateBatchExpiryDates(batches);
                if (!batchValidation.IsValid)
                {
                    await _auditService.LogAsync("store_indent", "SAVE_BATCH_EXP", indentItemId.ToString(), null, null,
                        $"Save batches validation failed - expiry date: {batchValidation.ErrorMessage}");
                    return Json(new { success = false, message = batchValidation.ErrorMessage });
                }

                // Validate each batch
                for (int i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    var batchNumber = i + 1;

                    // Validate received quantity
                    if (batch.ReceivedQuantity <= 0)
                    {
                        await _auditService.LogAsync("store_indent", "SAVE_BATCH_QTY", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - invalid quantity for batch {batchNumber}");
                        return Json(new { success = false, message = $"Received quantity must be greater than 0 for batch {batchNumber}." });
                    }

                    // Check if received quantity exceeds requested quantity
                    if (batch.ReceivedQuantity > requestedQuantity)
                    {
                        await _auditService.LogAsync("store_indent", "SAVE_BATCH_EXC", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - received exceeds requested for batch {batchNumber}");
                        return Json(new { success = false, message = $"Received quantity ({batch.ReceivedQuantity}) cannot exceed requested quantity ({requestedQuantity}) for batch {batchNumber}." });
                    }

                    // Validate available stock
                    if (batch.AvailableStock < 0)
                    {
                        await _auditService.LogAsync("store_indent", "SAVE_BATCH_STK", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - negative stock for batch {batchNumber}");
                        return Json(new { success = false, message = $"Available stock cannot be negative for batch {batchNumber}." });
                    }

                    if (batch.AvailableStock > batch.ReceivedQuantity)
                    {
                        await _auditService.LogAsync("store_indent", "SAVE_BATCH_AVL", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - available exceeds received for batch {batchNumber}");
                        return Json(new { success = false, message = $"Available stock ({batch.AvailableStock}) cannot exceed received quantity ({batch.ReceivedQuantity}) for batch {batchNumber}." });
                    }

                    // Validate batch number
                    if (string.IsNullOrWhiteSpace(batch.BatchNo))
                    {
                        await _auditService.LogAsync("store_indent", "SAVE_BATCH_VAL", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - empty batch number for batch {batchNumber}");
                        return Json(new { success = false, message = $"Batch number is required for batch {batchNumber}." });
                    }

                    // Validate expiry date
                    if (batch.ExpiryDate.Date < DateTime.Today)
                    {
                        await _auditService.LogAsync("store_indent", "SAVE_BATCH_EXP", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - past expiry date for batch {batch.BatchNo}");
                        return Json(new { success = false, message = $"Batch \"{batch.BatchNo}\" has an expiry date in the past. Please select today's date or a future date." });
                    }
                }

                // Additional validation: Check if total received quantity exceeds requested quantity
                var totalReceivedQuantity = batches.Sum(b => b.ReceivedQuantity);
                if (totalReceivedQuantity > requestedQuantity)
                {
                    await _auditService.LogAsync("store_indent", "SAVE_BATCH_TOT", indentItemId.ToString(), null, null,
                        $"Save batches validation failed - total received exceeds requested");
                    return Json(new { success = false, message = $"Total received quantity ({totalReceivedQuantity}) cannot exceed requested quantity ({requestedQuantity})." });
                }

                var batchEntities = batches.Select(b => new StoreIndentBatch
                {
                    BatchId = (b.BatchId ?? 0) > 0 ? b.BatchId.Value : 0,
                    IndentItemId = b.IndentItemId,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedQuantity = b.ReceivedQuantity,
                    VendorCode = b.VendorCode ?? "",
                    AvailableStock = b.AvailableStock
                }).ToList();

                await _repo.AddOrUpdateBatchesAsync(indentItemId, batchEntities, userPlantId);

                // Log successful batch save
                await _auditService.LogAsync("store_indent", "SAVE_BATCH_OK", indentItemId.ToString(), null, null,
                    $"Batches saved successfully - Count: {batchEntities.Count}, Total Received: {totalReceivedQuantity}, Plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    message = "Batches saved successfully!",
                    totalReceived = batchEntities.Sum(x => x.ReceivedQuantity),
                    totalAvailable = batchEntities.Sum(x => x.AvailableStock),
                    totalBatches = batchEntities.Count,
                    requestedQuantity = requestedQuantity
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "SAVE_BATCH_ERR", "unknown", null, null,
                    $"Save batches error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving batch information." });
            }
        }
        // Optional: Delete a single batch with audit logging and plant filtering
        [HttpPost]
        public async Task<IActionResult> DeleteBatch(int batchId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log batch delete attempt
                await _auditService.LogAsync("store_indent", "DELETE_BATCH", batchId.ToString(), null, null,
                    $"Batch deletion attempted: {batchId}, Plant: {userPlantId}");

                await _repo.DeleteBatchAsync(batchId, userPlantId);

                // Log successful batch deletion
                await _auditService.LogAsync("store_indent", "DELETE_BATCH_OK", batchId.ToString(),
                    new { BatchId = batchId }, null, $"Batch deleted successfully - Plant: {userPlantId}");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "DELETE_BATCH_E", batchId.ToString(), null, null,
                    $"Batch deletion error: {ex.Message}");
                return Json(new { success = false });
            }
        }

        #region Helper Methods

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
                await _auditService.LogAsync("store_indent", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        // Method to get user role with audit logging
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
                await _auditService.LogAsync("store_indent", "ROLE_ERROR", "system", null, null,
                    $"Error getting user role: {ex.Message}");
                return null;
            }
        }

        // Updated medicine processing methods with plant filtering
        private async Task<MedicineProcessResult> ProcessMedicinesForCreate(int indentId, string medicinesJson, int? userPlantId)
        {
            try
            {
                // Log medicine processing attempt
                await _auditService.LogAsync("store_indent", "PROC_MED_CRT", indentId.ToString(), null, null,
                    $"Medicine processing for create - IndentId: {indentId}, Plant: {userPlantId}");

                if (string.IsNullOrEmpty(medicinesJson))
                {
                    return new MedicineProcessResult { Success = true, HasMedicines = false };
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var medicines = JsonSerializer.Deserialize<List<MedicineDto>>(medicinesJson, options);

                if (medicines == null || !medicines.Any())
                {
                    return new MedicineProcessResult { Success = true, HasMedicines = false };
                }

                // Sanitize medicine data
                foreach (var med in medicines)
                {
                    med.VendorCode = SanitizeString(med.VendorCode ?? "");
                    med.BatchNo = SanitizeString(med.BatchNo);

                    // Only validate vendor code if it's not empty (optional field)
                    if (!string.IsNullOrWhiteSpace(med.VendorCode) && !IsVendorCodeSecure(med.VendorCode))
                    {
                        await _auditService.LogAsync("store_indent", "PROC_MED_VSEC", indentId.ToString(), null, null,
                            $"Medicine processing blocked - insecure vendor code: {med.VendorCode}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid vendor code format: {med.VendorCode}" };
                    }

                    if (!string.IsNullOrEmpty(med.BatchNo) && !IsBatchNoSecure(med.BatchNo))
                    {
                        await _auditService.LogAsync("store_indent", "PROC_MED_BSEC", indentId.ToString(), null, null,
                            $"Medicine processing blocked - insecure batch: {med.BatchNo}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid batch number format: {med.BatchNo}" };
                    }
                }

                var validationResult = await ValidateMedicines(indentId, medicines, userPlantId);
                if (!validationResult.Success)
                {
                    await _auditService.LogAsync("store_indent", "PROC_MED_VFAIL", indentId.ToString(), null, null,
                        $"Medicine validation failed: {validationResult.ErrorMessage}");
                    return validationResult;
                }

                // Add all medicines
                var newMedicines = medicines.Where(m => m.IsNew).ToList();

                foreach (var medicine in newMedicines)
                {
                    var item = new StoreIndentItem
                    {
                        IndentId = indentId,
                        MedItemId = medicine.MedItemId,
                        VendorCode = string.IsNullOrWhiteSpace(medicine.VendorCode) ? null : medicine.VendorCode, // Set to null if empty
                        RaisedQuantity = medicine.RaisedQuantity,  // Updated property name
                        ReceivedQuantity = medicine.ReceivedQuantity,  // New property
                        UnitPrice = medicine.UnitPrice,  // Add this
                        TotalAmount = medicine.TotalAmount ?? (medicine.UnitPrice * medicine.ReceivedQuantity)  // Add this
                    };

                    await _repo.AddItemAsync(item);
                }

                // Log successful processing
                await _auditService.LogAsync("store_indent", "PROC_MED_OK", indentId.ToString(), null, null,
                    $"Medicine processing successful - Added: {newMedicines.Count} medicines, Plant: {userPlantId}");
                return new MedicineProcessResult { Success = true, HasMedicines = true };
            }
            catch (Exception ex)
            {
                // Log medicine processing failure
                await _auditService.LogAsync("store_indent", "PROC_MED_FAIL", indentId.ToString(), null, null,
                    $"Medicine processing failed: {ex.Message}");
                return new MedicineProcessResult { Success = false, ErrorMessage = $"Error processing medicines data: {ex.Message}" };
            }
        }

        private async Task<MedicineProcessResult> ProcessMedicinesForEdit(int indentId, string medicinesJson, int? userPlantId)
        {
            try
            {
                // Log medicine processing attempt for edit
                await _auditService.LogAsync("store_indent", "PROC_MED_EDT", indentId.ToString(), null, null,
                    $"Medicine processing for edit - IndentId: {indentId}, Plant: {userPlantId}");

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var medicines = string.IsNullOrEmpty(medicinesJson)
                    ? new List<MedicineDto>()
                    : JsonSerializer.Deserialize<List<MedicineDto>>(medicinesJson, options);

                if (medicines == null)
                {
                    medicines = new List<MedicineDto>();
                }

                // Sanitize medicine data
                foreach (var med in medicines)
                {
                    med.VendorCode = SanitizeString(med.VendorCode ?? "");
                    med.BatchNo = SanitizeString(med.BatchNo);

                    // Only validate vendor code if it's not empty (optional field)
                    if (!string.IsNullOrWhiteSpace(med.VendorCode) && !IsVendorCodeSecure(med.VendorCode))
                    {
                        await _auditService.LogAsync("store_indent", "PROC_EDT_VSEC", indentId.ToString(), null, null,
                            $"Edit medicine processing blocked - insecure vendor code: {med.VendorCode}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid vendor code format: {med.VendorCode}" };
                    }

                    if (!string.IsNullOrEmpty(med.BatchNo) && !IsBatchNoSecure(med.BatchNo))
                    {
                        await _auditService.LogAsync("store_indent", "PROC_EDT_BSEC", indentId.ToString(), null, null,
                            $"Edit medicine processing blocked - insecure batch: {med.BatchNo}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid batch number format: {med.BatchNo}" };
                    }
                }

                // Get existing medicines with plant filtering
                var existingItems = await _repo.GetItemsByIndentIdAsync(indentId, userPlantId);
                var existingIds = existingItems.Select(x => x.IndentItemId).ToList();
                var submittedExistingIds = medicines.Where(m => !m.IsNew && m.IndentItemId.HasValue)
                                                  .Select(m => m.IndentItemId.Value).ToList();

                // Delete removed medicines
                var toDelete = existingIds.Except(submittedExistingIds).ToList();
                foreach (var deleteId in toDelete)
                {
                    await _repo.DeleteItemAsync(deleteId, userPlantId);
                    // Log medicine deletion during edit
                    await _auditService.LogAsync("store_indent", "PROC_EDT_DEL", deleteId.ToString(), null, null,
                        $"Medicine deleted during edit - ItemId: {deleteId}, Plant: {userPlantId}");
                }

                // Validate remaining medicines
                var validationResult = await ValidateMedicines(indentId, medicines, userPlantId, submittedExistingIds);
                if (!validationResult.Success)
                {
                    await _auditService.LogAsync("store_indent", "PROC_EDT_VFAIL", indentId.ToString(), null, null,
                        $"Edit medicine validation failed: {validationResult.ErrorMessage}");
                    return validationResult;
                }

                // Update existing medicines
                foreach (var medicine in medicines.Where(m => !m.IsNew && m.IndentItemId.HasValue))
                {
                    var existingItem = existingItems.FirstOrDefault(x => x.IndentItemId == medicine.IndentItemId.Value);
                    if (existingItem != null)
                    {
                        existingItem.VendorCode = string.IsNullOrWhiteSpace(medicine.VendorCode) ? null : medicine.VendorCode;
                        existingItem.RaisedQuantity = medicine.RaisedQuantity;  // Updated property name
                        existingItem.ReceivedQuantity = medicine.ReceivedQuantity;  // New property
                        existingItem.UnitPrice = medicine.UnitPrice;  // Add this
                        existingItem.TotalAmount = medicine.TotalAmount ?? (medicine.UnitPrice * medicine.ReceivedQuantity);

                        await _repo.UpdateItemAsync(existingItem);
                    }
                }

                // Add new medicines
                var newMedicinesCount = 0;
                foreach (var medicine in medicines.Where(m => m.IsNew))
                {
                    var item = new StoreIndentItem
                    {
                        IndentId = indentId,
                        MedItemId = medicine.MedItemId,
                        VendorCode = string.IsNullOrWhiteSpace(medicine.VendorCode) ? null : medicine.VendorCode,
                        RaisedQuantity = medicine.RaisedQuantity,  // Updated property name
                        ReceivedQuantity = medicine.ReceivedQuantity,  // New property
                        UnitPrice = medicine.UnitPrice,  // Add this
                        TotalAmount = medicine.TotalAmount ?? (medicine.UnitPrice * medicine.ReceivedQuantity)  // Add this
                    };

                    await _repo.AddItemAsync(item);
                    newMedicinesCount++;
                }

                // Log successful edit processing
                await _auditService.LogAsync("store_indent", "PROC_EDT_OK", indentId.ToString(), null, null,
                    $"Edit medicine processing successful - Added: {newMedicinesCount}, Deleted: {toDelete.Count}, Plant: {userPlantId}");

                return new MedicineProcessResult { Success = true, HasMedicines = true };
            }
            catch (Exception ex)
            {
                // Log edit medicine processing failure
                await _auditService.LogAsync("store_indent", "PROC_EDT_FAIL", indentId.ToString(), null, null,
                    $"Edit medicine processing failed: {ex.Message}");
                return new MedicineProcessResult { Success = false, ErrorMessage = $"Error processing medicines data: {ex.Message}" };
            }
        }

        // Updated ValidateMedicines method with plant filtering
        private async Task<MedicineProcessResult> ValidateMedicines(int indentId, List<MedicineDto> medicines, int? userPlantId, List<int> excludeItemIds = null)
        {
            try
            {
                // Log validation attempt
                await _auditService.LogAsync("store_indent", "VALID_MED", indentId.ToString(), null, null,
                    $"Medicine validation started - Count: {medicines.Count}, Plant: {userPlantId}");

                var medicineIds = new HashSet<int>();

                foreach (var medicine in medicines)
                {
                    // Vendor code is now optional - only validate format if provided
                    if (!string.IsNullOrWhiteSpace(medicine.VendorCode) && !IsVendorCodeSecure(medicine.VendorCode))
                    {
                        await _auditService.LogAsync("store_indent", "VALID_MED_FMT", indentId.ToString(), null, null,
                            $"Medicine validation failed - invalid vendor code format: {medicine.VendorCode}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid vendor code format: {medicine.VendorCode}. Only letters, numbers, hyphens, and underscores are allowed." };
                    }

                    // Check for duplicate medicines within the submission
                    if (medicineIds.Contains(medicine.MedItemId))
                    {
                        await _auditService.LogAsync("store_indent", "VALID_MED_DUP", indentId.ToString(), null, null,
                            $"Medicine validation failed - duplicate in submission: {medicine.MedItemId}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Duplicate medicine found in medicines list." };
                    }
                    medicineIds.Add(medicine.MedItemId);

                    // Check against existing data (excluding items being updated) - ONLY check for same medicine, NOT vendor code
                    // Include plant filtering in this check
                    var excludeId = medicine.IsNew ? null : medicine.IndentItemId;

                    if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medicine.MedItemId, excludeId, userPlantId))
                    {
                        await _auditService.LogAsync("store_indent", "VALID_MED_EXIST", indentId.ToString(), null, null,
                            $"Medicine validation failed - already exists: {medicine.MedItemId}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = "One or more medicines are already added to this indent." };
                    }
                }

                // Log successful validation
                await _auditService.LogAsync("store_indent", "VALID_MED_OK", indentId.ToString(), null, null,
                    $"Medicine validation successful - Count: {medicines.Count}, Plant: {userPlantId}");

                return new MedicineProcessResult { Success = true };
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("store_indent", "VALID_MED_ERR", indentId.ToString(), null, null,
                    $"Medicine validation error: {ex.Message}");
                throw;
            }
        }

        // Updated PopulateMedicineDropdownAsync to show ID and Name with plant-wise filtering
        private async Task PopulateMedicineDropdownAsync()
        {
            // Get current user's plant ID for filtering
            var userPlantId = await GetCurrentUserPlantIdAsync();

            // Get medicines filtered by user's plant
            var medicines = await _repo.GetMedicinesAsync(userPlantId);

            // Create a custom list with ID and Name combined
            var medicineSelectList = medicines.Select(m => new SelectListItem
            {
                Value = m.MedItemId.ToString(),
                Text = $"{m.MedItemId} - {m.MedItemName}"
            }).OrderBy(m => m.Text).ToList();

            // Add a default "Select Medicine" option at the beginning
            medicineSelectList.Insert(0, new SelectListItem
            {
                Value = "",
                Text = "Select Medicine"
            });

            ViewBag.MedicineList = medicineSelectList;
        }
        // New method to populate indent type dropdown with proper filtering
        private async Task PopulateIndentTypeDropdownAsync(string createdBy = null)
        {
            var currentUser = User.Identity?.Name + " - " + User.GetFullName();
            var indentTypes = new List<SelectListItem>();

            // Standard options available to all users
            indentTypes.Add(new SelectListItem { Value = "Pending Indents", Text = "Pending Indents" });
            indentTypes.Add(new SelectListItem { Value = "Approved Indents", Text = "Approved Indents" });
            indentTypes.Add(new SelectListItem { Value = "Rejected Indents", Text = "Rejected Indents" });

            // Draft option - only show to the creator or for new items
            if (string.IsNullOrEmpty(createdBy) || createdBy == currentUser)
            {
                indentTypes.Add(new SelectListItem { Value = "Draft Indent", Text = "Draft Indent" });
            }

            ViewBag.IndentTypeList = indentTypes;
        }

        /// <summary>
        /// Sets the Status property based on the IndentType
        /// </summary>
        private void SetStatusBasedOnIndentType(StoreIndent model)
        {
            model.Status = model.IndentType switch
            {
                "Pending Indents" => "Pending",
                "Approved Indents" => "Approved",
                "Rejected Indents" => "Rejected",
                "Draft Indent" => "Draft",
                _ => "Pending" // Default fallback
            };
        }

        #region Enhanced Expiry Date Validation Methods

        /// <summary>
        /// Validates expiry date to ensure it's not in the past
        /// </summary>
        /// <param name="expiryDate">The expiry date to validate</param>
        /// <param name="fieldName">The field name for error messaging</param>
        /// <returns>Validation result with success status and error message</returns>
        private (bool IsValid, string ErrorMessage) ValidateExpiryDate(DateTime? expiryDate, string fieldName = "Expiry date")
        {
            if (expiryDate == null)
                return (true, string.Empty); // Null dates are allowed

            if (expiryDate.Value.Date < DateTime.Today)
            {
                return (false, $"{fieldName} cannot be in the past. Please select today's date or a future date.");
            }

            // Additional validation: Check if expiry date is too far in the future (optional business rule)
            var maxExpiryDate = DateTime.Today.AddYears(10); // 10 years maximum
            if (expiryDate.Value.Date > maxExpiryDate)
            {
                return (false, $"{fieldName} cannot be more than 10 years in the future.");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Validates a list of batch expiry dates
        /// </summary>
        /// <param name="batches">List of batches to validate</param>
        /// <returns>Validation result</returns>
        private (bool IsValid, string ErrorMessage) ValidateBatchExpiryDates(List<StoreIndentBatchDto> batches)
        {
            if (batches == null || !batches.Any())
                return (true, string.Empty);

            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                var validation = ValidateExpiryDate(batch.ExpiryDate, $"Batch {i + 1} expiry date");

                if (!validation.IsValid)
                {
                    return (false, $"Batch '{batch.BatchNo}': {validation.ErrorMessage}");
                }
            }

            return (true, string.Empty);
        }

        #endregion

        #region Private Methods for Input Sanitization and Validation

        private StoreIndent SanitizeInput(StoreIndent model)
        {
            if (model == null) return model;

            model.IndentType = SanitizeString(model.IndentType);
            model.CreatedBy = SanitizeString(model.CreatedBy);
            model.Status = SanitizeString(model.Status);
            model.Comments = SanitizeString(model.Comments);
            model.ApprovedBy = SanitizeString(model.ApprovedBy);

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

        private bool IsInputSecure(StoreIndent model)
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

            var inputsToCheck = new[] { model.IndentType, model.CreatedBy, model.Status, model.Comments, model.ApprovedBy };

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

        private bool IsCommentsSecure(string comments)
        {
            if (string.IsNullOrEmpty(comments)) return true;

            // Check for potentially dangerous patterns in comments
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
                @"<embed"
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (Regex.IsMatch(comments, pattern, RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        // Updated IsVendorCodeSecure method - now optional
        private bool IsVendorCodeSecure(string vendorCode)
        {
            // If null, empty, or whitespace - it's valid because vendor code is optional
            if (string.IsNullOrWhiteSpace(vendorCode))
                return true;

            // If provided, validate format and minimum length
            return vendorCode.Length >= 2 && Regex.IsMatch(vendorCode, @"^[a-zA-Z0-9\-_]+$");
        }

        private bool IsBatchNoSecure(string batchNo)
        {
            if (string.IsNullOrEmpty(batchNo)) return true;

            // Batch number should only contain alphanumeric characters, hyphens, underscores, and dots
            return Regex.IsMatch(batchNo, @"^[a-zA-Z0-9\-_\.]+$");
        }

        #endregion

        #endregion

        // DTO classes
        public class StoreIndentBatchDto
        {
            public int? BatchId { get; set; }
            public int IndentItemId { get; set; }
            public string BatchNo { get; set; }
            public DateTime ExpiryDate { get; set; }
            public int ReceivedQuantity { get; set; }
            public string VendorCode { get; set; }
            public int AvailableStock { get; set; } = 0; // NEW FIELD
        }

        // Updated MedicineDto class - vendor code is now optional
        public class MedicineDto
        {
            public int? IndentItemId { get; set; }
            public string? TempId { get; set; }
            public int MedItemId { get; set; }
            public string MedItemName { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;

            [StringLength(50, ErrorMessage = "Vendor code cannot exceed 50 characters")]
            [RegularExpression(@"^[a-zA-Z0-9\-_]*$", ErrorMessage = "Vendor code can only contain letters, numbers, hyphens, and underscores")]
            public string? VendorCode { get; set; }

            public int RaisedQuantity { get; set; }
            public int ReceivedQuantity { get; set; }
            public decimal? UnitPrice { get; set; }  // Add this
            public decimal? TotalAmount { get; set; }  // Add this
            public string? BatchNo { get; set; }
            public bool IsNew { get; set; } = true;
        }
        // Helper class for medicine processing results
        public class MedicineProcessResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public bool HasMedicines { get; set; } = false;
        }
    }
}