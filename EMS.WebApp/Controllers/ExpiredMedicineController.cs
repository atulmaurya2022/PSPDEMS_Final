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
    [Authorize("AccessExpiredMedicine")]
    public class ExpiredMedicineController : Controller
    {
        private readonly IExpiredMedicineRepository _repo;
        private readonly IAuditService _auditService;
        private readonly ILogger<ExpiredMedicineController> _logger;

        public ExpiredMedicineController(IExpiredMedicineRepository repo, IAuditService auditService, ILogger<ExpiredMedicineController> logger)
        {
            _repo = repo;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();

                // Log index access for security monitoring
                await _auditService.LogAsync("expired_medicine", "INDEX_VIEW", "main", null, null,
                    $"Expired medicine module accessed, Plant: {userPlantId}, Role: {userRole}");

                // Pass user role to view for UI customization
                ViewBag.UserRole = userRole;
                ViewBag.UserPlantId = userPlantId;

                // Get accessible source types for the user
                var accessibleSourceTypes = await _repo.GetAccessibleSourceTypesAsync(userRole);
                ViewBag.AccessibleSourceTypes = accessibleSourceTypes;

                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("expired_medicine", "INDEX_FAILED", "main", null, null,
                    $"Failed to load expired medicine index: {ex.Message}");
                throw;
            }
        }

        // ======= UPDATED: Load data for DataTables WITH PLANT FILTERING AND ROLE-BASED ACCESS =======
        public async Task<IActionResult> LoadData(string status = "pending", DateTime? fromDate = null, DateTime? toDate = null, string? sourceType = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();
                var currentUser = GetCurrentUserIdentifier();  // NEW: Get current user

                // DEBUG: Log what we're searching for
                _logger.LogInformation($"LoadData called - Status: {status}, SourceType: {sourceType}, UserRole: '{userRole}', PlantId: {userPlantId}");

                // DEBUG: Let's also test the statistics method that works
                var testStats = await _repo.GetStatisticsBySourceTypeAsync(userPlantId, userRole, currentUser);
                _logger.LogInformation($"DEBUG - Statistics by source type: {string.Join(", ", testStats.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");

                IEnumerable<ExpiredMedicine> expiredMedicines;

                if (status.ToLower() == "disposed")
                {
                    expiredMedicines = await _repo.ListDisposedAsync(fromDate, toDate, userPlantId, userRole, currentUser);
                }
                else
                {
                    expiredMedicines = await _repo.ListPendingDisposalAsync(userPlantId, userRole, currentUser);
                }

                // DEBUG: Log what we found before filtering
                _logger.LogInformation($"Found {expiredMedicines.Count()} medicines before source filtering");

                // DEBUG: Log what source types we found
                var foundSourceTypes = expiredMedicines.GroupBy(e => e.SourceType).Select(g => $"{g.Key}: {g.Count()}").ToList();
                _logger.LogInformation($"Found source types: {string.Join(", ", foundSourceTypes)}");

                // Apply source type filtering if specified
                if (!string.IsNullOrEmpty(sourceType))
                {
                    expiredMedicines = expiredMedicines.Where(e => e.SourceType.Equals(sourceType, StringComparison.OrdinalIgnoreCase));
                    _logger.LogInformation($"After source filtering ({sourceType}): {expiredMedicines.Count()} medicines");
                }

                var result = expiredMedicines.Select((item, index) => new
                {
                    slNo = index + 1,
                    expiredMedicineId = item.ExpiredMedicineId,
                    medicineName = item.MedicineName ?? "Unknown",
                    companyName = item.CompanyName ?? "Not Defined",
                    batchNumber = item.BatchNumber ?? "N/A",
                    vendorCode = item.VendorCode ?? "N/A",
                    expiredOn = item.ExpiryDate.ToString("dd/MM/yyyy"),
                    daysOverdue = item.DaysOverdue,
                    qtyExpired = item.QuantityExpired ?? 0,
                    unitPrice = item.UnitPrice?.ToString("C") ?? "N/A",
                    totalValue = item.TotalValue?.ToString("C") ?? "N/A",
                    indentNo = item.IndentNumber ?? item.IndentId.ToString(),
                    status = item.Status ?? "Unknown",
                    priorityLevel = item.PriorityLevel,
                    detectedDate = item.DetectedDate.ToString("dd/MM/yyyy"),
                    issuedDate = item.BiomedicalWasteIssuedDate?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    issuedBy = item.BiomedicalWasteIssuedBy ?? "",
                    typeOfMedicine = item.TypeOfMedicine ?? "Select Type of Medicine",
                    typeBadgeClass = item.TypeBadgeClass,
                    isDisposed = item.IsDisposed,
                    isCritical = item.IsCritical,
                    canDispose = item.TypeOfMedicine != "Select Type of Medicine",
                    plantName = item.OrgPlant?.plant_name ?? "Unknown Plant",
                    sourceType = item.SourceType,
                    sourceDisplay = item.SourceDisplay,
                    sourceBadgeClass = item.SourceBadgeClass
                }).ToList();
                _logger.LogInformation($"Returning {result.Count} medicines to frontend");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoadData");
                return Json(new
                {
                    data = new List<object>(),
                    error = $"Error: {ex.Message}",
                    details = ex.InnerException?.Message ?? "No inner exception"
                });
            }
        }
        [HttpPost]
        public async Task<IActionResult> IssueToBiomedicalWaste(int id, string? remarks = null)
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();
                var currentUser = GetCurrentUserIdentifier();

                //var currentUser = User.Identity?.Name;
                if (string.IsNullOrEmpty(currentUser))
                {
                    await _auditService.LogAsync("expired_medicine", "ISSUE_NO_USER", id.ToString(), null, null,
                        "Issue to biomedical waste failed - user not identified");
                    return Json(new { success = false, message = "User not identified." });
                }

                // Log issue attempt (critical operation)
                await _auditService.LogAsync("expired_medicine", "ISSUE_ATTEMPT", id.ToString(), null, null,
                    $"Issue to biomedical waste attempted for ID: {id}, Plant: {userPlantId}, Role: {userRole}");

                // Get the item to verify it's pending disposal WITH PLANT AND ROLE FILTERING
                var item = await _repo.GetByIdAsync(id, userPlantId, userRole, currentUser);
                if (item == null)
                {
                    await _auditService.LogAsync("expired_medicine", "ISSUE_NOTFOUND", id.ToString(), null, null,
                        "Issue failed - expired medicine record not found or access denied");
                    return Json(new { success = false, message = "Expired medicine record not found or access denied." });
                }

                if (item.Status != "Pending Disposal")
                {
                    await _auditService.LogAsync("expired_medicine", "ISSUE_ALREADY", id.ToString(), null, null,
                        $"Issue failed - medicine already disposed: {item.Status}");
                    return Json(new { success = false, message = "Medicine has already been disposed of." });
                }

                // Validate medicine type before disposal
                if (item.TypeOfMedicine == "Select Type of Medicine" || string.IsNullOrEmpty(item.TypeOfMedicine))
                {
                    await _auditService.LogAsync("expired_medicine", "ISSUE_NO_TYPE", id.ToString(), null, null,
                        "Issue failed - medicine type not selected");
                    return Json(new
                    {
                        success = false,
                        message = "Please select a valid medicine type (Solid, Liquid, or Gel) before disposing this medicine.",
                        requiresTypeSelection = true
                    });
                }

                // Validate user can dispose this source type
                if (!await _repo.CanUserAccessSourceTypeAsync(item.SourceType, userRole))
                {
                    await _auditService.LogAsync("expired_medicine", "ISSUE_NO_ACCESS", id.ToString(), null, null,
                        $"Issue failed - user role '{userRole}' cannot dispose '{item.SourceType}' medicines");
                    return Json(new { success = false, message = $"You do not have permission to dispose {item.SourceType} medicines." });
                }

                // Issue to biomedical waste WITH PLANT AND ROLE FILTERING
                await _repo.IssueToBiomedicalWasteAsync(id, currentUser, userPlantId, userRole, remarks);

                // Log successful issue (critical operation)
                await _auditService.LogUpdateAsync("expired_medicine", id.ToString(),
                    null, new { Status = "Issued to Biomedical Waste", IssuedBy = currentUser, PlantId = userPlantId, SourceType = item.SourceType },
                    $"Medicine issued to biomedical waste successfully by: {currentUser}, Plant: {userPlantId}, Role: {userRole}");

                return Json(new
                {
                    success = true,
                    message = $"Medicine successfully issued to biomedical waste from {item.SourceType}.",
                    issuedDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    issuedBy = currentUser,
                    sourceType = item.SourceType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error issuing medicine {id} to biomedical waste");
                await _auditService.LogAsync("expired_medicine", "ISSUE_ERROR", id.ToString(), null, null,
                    $"Issue to biomedical waste failed with error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while issuing to biomedical waste." });
            }
        }

        // ======= UPDATED: Bulk issue to biomedical waste WITH ROLE-BASED ACCESS =======
        [HttpPost]
        public async Task<IActionResult> BulkIssueToBiomedicalWaste(string ids, string? remarks = null)
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();

                var currentUser = User.Identity?.Name;
                if (string.IsNullOrEmpty(currentUser))
                {
                    await _auditService.LogAsync("expired_medicine", "BULK_NO_USER", "bulk", null, null,
                        "Bulk issue failed - user not identified");
                    return Json(new { success = false, message = "User not identified." });
                }

                if (string.IsNullOrEmpty(ids))
                {
                    await _auditService.LogAsync("expired_medicine", "BULK_NO_ITEMS", "bulk", null, null,
                        "Bulk issue failed - no items selected");
                    return Json(new { success = false, message = "No items selected." });
                }

                var idList = ids.Split(',').Select(int.Parse).ToList();

                // Log bulk issue attempt (critical operation)
                await _auditService.LogAsync("expired_medicine", "BULK_ATTEMPT", "bulk", null, null,
                    $"Bulk issue to biomedical waste attempted for {idList.Count} items, Plant: {userPlantId}, Role: {userRole}");

                // Validate all selected items have proper medicine types AND user has access WITH ROLE FILTERING
                var itemsToValidate = new List<ExpiredMedicine>();
                foreach (var id in idList)
                {
                    var item = await _repo.GetByIdAsync(id, userPlantId, userRole);
                    if (item != null)
                    {
                        itemsToValidate.Add(item);
                    }
                }

                // Check for items with invalid medicine types
                var invalidTypeItems = itemsToValidate
                    .Where(item => item.TypeOfMedicine == "Select Type of Medicine" || string.IsNullOrEmpty(item.TypeOfMedicine))
                    .ToList();

                if (invalidTypeItems.Any())
                {
                    var invalidMedicineNames = invalidTypeItems.Select(item => $"• {item.MedicineName} (Batch: {item.BatchNumber}) - {item.SourceType}").ToList();
                    var invalidItemsMessage = string.Join("\n", invalidMedicineNames);

                    await _auditService.LogAsync("expired_medicine", "BULK_INVALID_TYPE", "bulk", null, null,
                        $"Bulk issue failed - {invalidTypeItems.Count} items need type selection");

                    return Json(new
                    {
                        success = false,
                        message = $"The following medicines need to have their type selected (Solid, Liquid, or Gel) before disposal:\n\n{invalidItemsMessage}\n\nPlease update the medicine types and try again.",
                        requiresTypeSelection = true,
                        invalidItems = invalidTypeItems.Select(item => new {
                            id = item.ExpiredMedicineId,
                            name = item.MedicineName,
                            batch = item.BatchNumber,
                            sourceType = item.SourceType
                        }).ToList()
                    });
                }

                // Check for items user cannot dispose based on role
                var unauthorizedItems = itemsToValidate
                    .Where(item => !_repo.CanUserAccessSourceTypeAsync(item.SourceType, userRole).Result)
                    .ToList();

                if (unauthorizedItems.Any())
                {
                    var unauthorizedMedicineNames = unauthorizedItems.Select(item => $"• {item.MedicineName} (Batch: {item.BatchNumber}) - {item.SourceType}").ToList();
                    var unauthorizedItemsMessage = string.Join("\n", unauthorizedMedicineNames);

                    await _auditService.LogAsync("expired_medicine", "BULK_UNAUTHORIZED", "bulk", null, null,
                        $"Bulk issue failed - user lacks permission for {unauthorizedItems.Count} items");

                    return Json(new
                    {
                        success = false,
                        message = $"You do not have permission to dispose the following medicines:\n\n{unauthorizedItemsMessage}\n\nPlease contact your administrator for access.",
                        unauthorizedItems = unauthorizedItems.Select(item => new {
                            id = item.ExpiredMedicineId,
                            name = item.MedicineName,
                            batch = item.BatchNumber,
                            sourceType = item.SourceType
                        }).ToList()
                    });
                }

                // Bulk issue to biomedical waste WITH PLANT AND ROLE FILTERING
                await _repo.BulkIssueToBiomedicalWasteAsync(idList, currentUser, userPlantId, userRole, remarks);

                // Count by source type for detailed reporting
                var sourceTypeCounts = itemsToValidate.GroupBy(item => item.SourceType).ToDictionary(g => g.Key, g => g.Count());
                var sourceTypeMessage = string.Join(", ", sourceTypeCounts.Select(kvp => $"{kvp.Value} {kvp.Key}"));

                // Log successful bulk issue (critical operation)
                await _auditService.LogUpdateAsync("expired_medicine", "bulk",
                    null, new { Status = "Issued to Biomedical Waste", IssuedBy = currentUser, Count = idList.Count, PlantId = userPlantId, Role = userRole },
                    $"Bulk issue to biomedical waste successful - {idList.Count} items ({sourceTypeMessage}) by: {currentUser}, Plant: {userPlantId}, Role: {userRole}");

                return Json(new
                {
                    success = true,
                    message = $"Successfully issued {idList.Count} items to biomedical waste ({sourceTypeMessage}).",
                    issuedDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    issuedBy = currentUser,
                    count = idList.Count,
                    sourceTypeCounts = sourceTypeCounts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk issue to biomedical waste");
                await _auditService.LogAsync("expired_medicine", "BULK_ERROR", "bulk", null, null,
                    $"Bulk issue failed with error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while processing bulk issue." });
            }
        }

        // ======= UPDATED: View details WITH ROLE-BASED ACCESS =======
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();

                // Log details view attempt
                await _auditService.LogAsync("expired_medicine", "DETAILS_VIEW", id.ToString(), null, null,
                    $"Details view attempted for plant: {userPlantId}, Role: {userRole}");

                // Get item WITH ROLE FILTERING
                var item = await _repo.GetByIdWithDetailsAsync(id, userPlantId, userRole);
                if (item == null)
                {
                    await _auditService.LogAsync("expired_medicine", "DETAILS_NOTFND", id.ToString(), null, null,
                        $"Details view failed - item not found or access denied for plant: {userPlantId}, Role: {userRole}");
                    return NotFound();
                }

                // Log successful details view
                await _auditService.LogViewAsync("expired_medicine", id.ToString(),
                    $"Expired medicine details viewed - Medicine: {item.MedicineName}, Source: {item.SourceType}, Plant: {item.OrgPlant?.plant_name}");

                return PartialView("_Details", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("expired_medicine", "DETAILS_ERROR", id.ToString(), null, null,
                    $"Details view error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading details." });
            }
        }

        // ======= UPDATED: Sync expired medicines manually WITH ROLE-BASED ACCESS =======
        [HttpPost]
        public async Task<IActionResult> SyncExpiredMedicines(string? sourceType = null)
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();

                var currentUser = User.Identity?.Name ?? "System";

                // Log sync attempt
                await _auditService.LogAsync("expired_medicine", "SYNC_ATTEMPT", "system", null, null,
                    $"Manual sync attempted by: {currentUser}, Plant: {userPlantId}, Role: {userRole}, SourceType: {sourceType}");

                // Validate user can sync the requested source type
                if (!string.IsNullOrEmpty(sourceType) && !await _repo.CanUserAccessSourceTypeAsync(sourceType, userRole))
                {
                    return Json(new { success = false, message = $"You do not have permission to sync {sourceType} medicines." });
                }

                // Sync WITH ROLE-BASED FILTERING
                var newItems = await _repo.DetectNewExpiredMedicinesAsync(currentUser, userPlantId, sourceType);

                if (newItems.Any())
                {
                    foreach (var item in newItems)
                    {
                        await _repo.AddAsync(item);
                    }

                    // Count by source type
                    var sourceTypeCounts = newItems.GroupBy(item => item.SourceType).ToDictionary(g => g.Key, g => g.Count());
                    var sourceTypeMessage = string.Join(", ", sourceTypeCounts.Select(kvp => $"{kvp.Value} {kvp.Key}"));

                    // Log successful sync
                    await _auditService.LogAsync("expired_medicine", "SYNC_SUCCESS", "system", null, null,
                        $"Manual sync successful - {newItems.Count} new items detected ({sourceTypeMessage}) by: {currentUser}, Plant: {userPlantId}, Role: {userRole}");

                    return Json(new
                    {
                        success = true,
                        message = $"Successfully detected and added {newItems.Count} new expired medicines ({sourceTypeMessage}).",
                        count = newItems.Count,
                        sourceTypeCounts = sourceTypeCounts
                    });
                }
                else
                {
                    await _auditService.LogAsync("expired_medicine", "SYNC_NONE", "system", null, null,
                        $"Manual sync completed - no new items detected, Plant: {userPlantId}, Role: {userRole}");

                    return Json(new
                    {
                        success = true,
                        message = "No new expired medicines detected.",
                        count = 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing expired medicines");
                await _auditService.LogAsync("expired_medicine", "SYNC_ERROR", "system", null, null,
                    $"Manual sync failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while syncing expired medicines." });
            }
        }

        // ======= UPDATED: Get statistics for dashboard WITH ROLE-BASED ACCESS =======
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();

                // Log statistics access
                await _auditService.LogAsync("expired_medicine", "STATS_ACCESS", "system", null, null,
                    $"Statistics accessed for plant: {userPlantId}, Role: {userRole}");

                // Get statistics WITH ROLE-BASED FILTERING
                var stats = new
                {
                    totalExpired = await _repo.GetTotalExpiredCountAsync(userPlantId, userRole),
                    pendingDisposal = await _repo.GetPendingDisposalCountAsync(userPlantId, userRole),
                    disposed = await _repo.GetDisposedCountAsync(userPlantId, userRole),
                    totalValue = await _repo.GetTotalExpiredValueAsync(userPlantId, userRole),
                    criticalCount = (await _repo.GetCriticalExpiredMedicinesAsync(userPlantId, userRole)).Count(),
                    sourceTypeStats = await _repo.GetStatisticsBySourceTypeAsync(userPlantId, userRole)
                };

                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading statistics");
                await _auditService.LogAsync("expired_medicine", "STATS_ERROR", "system", null, null,
                    $"Statistics loading failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading statistics." });
            }
        }

        // ======= UPDATED: Print report WITH ROLE-BASED ACCESS =======
        public async Task<IActionResult> PrintReport(string ids)
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();

                if (string.IsNullOrEmpty(ids))
                {
                    return Json(new { success = false, message = "No items selected for printing." });
                }

                var idList = ids.Split(',').Select(int.Parse).ToList();

                // Log print report access
                await _auditService.LogAsync("expired_medicine", "PRINT_REPORT", "bulk", null, null,
                    $"Print report accessed for {idList.Count} items, Plant: {userPlantId}, Role: {userRole}");

                // Get items WITH ROLE FILTERING
                var items = await _repo.GetExpiredMedicinesForPrintAsync(idList, userPlantId, userRole);

                if (!items.Any())
                {
                    return Json(new { success = false, message = "No valid items found for printing or access denied." });
                }

                return PartialView("_PrintReport", items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating print report");
                return Json(new { success = false, message = "An error occurred while generating the print report." });
            }
        }

        // ======= UPDATED: Update medicine type inline WITH ROLE-BASED ACCESS =======
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineType(int id, string typeOfMedicine)
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();
                var currentUser = GetCurrentUserIdentifier();

                // Validate the medicine type
                var validTypes = new[] { "Select Type of Medicine", "Solid", "Liquid", "Gel" };
                if (!validTypes.Contains(typeOfMedicine))
                {
                    return Json(new { success = false, message = "Invalid medicine type." });
                }

                // Log update attempt
                await _auditService.LogAsync("expired_medicine", "UPDATE_TYPE", id.ToString(), null, null,
                    $"Medicine type update attempted - ID: {id}, Type: {typeOfMedicine}, Plant: {userPlantId}, Role: {userRole}");

                // Get the item to verify it exists and is editable WITH ROLE FILTERING
                var item = await _repo.GetByIdAsync(id, userPlantId, userRole, currentUser);
                if (item == null)
                {
                    await _auditService.LogAsync("expired_medicine", "UPDATE_NOTFOUND", id.ToString(), null, null,
                        "Medicine type update failed - record not found or access denied");
                    return Json(new { success = false, message = "Expired medicine record not found or access denied." });
                }

                // Update the medicine type WITH ROLE FILTERING
                await _repo.UpdateMedicineTypeAsync(id, typeOfMedicine, userPlantId, userRole);

                // Get the badge class for the new type
                var badgeClass = typeOfMedicine.ToLower() switch
                {
                    "liquid" => "bg-info",
                    "solid" => "bg-success",
                    "gel" => "bg-warning text-dark",
                    "select type of medicine" => "bg-secondary",
                    _ => "bg-secondary"
                };

                // Log successful update
                await _auditService.LogUpdateAsync("expired_medicine", id.ToString(),
                    new { TypeOfMedicine = item.TypeOfMedicine }, new { TypeOfMedicine = typeOfMedicine },
                    $"Medicine type updated successfully to: {typeOfMedicine}, Source: {item.SourceType}, Plant: {userPlantId}, Role: {userRole}");

                return Json(new
                {
                    success = true,
                    message = $"Medicine type updated successfully for {item.SourceType} medicine.",
                    typeOfMedicine = typeOfMedicine,
                    badgeClass = badgeClass,
                    canDispose = typeOfMedicine != "Select Type of Medicine",
                    sourceType = item.SourceType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating medicine type for {id}");
                await _auditService.LogAsync("expired_medicine", "UPDATE_ERROR", id.ToString(), null, null,
                    $"Medicine type update failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating medicine type." });
            }
        }

        // Get medicine types for dropdown
        public IActionResult GetMedicineTypes()
        {
            try
            {
                var types = ExpiredMedicine.MedicineTypes.Select(type => new
                {
                    value = type,
                    text = type,
                    badgeClass = type.ToLower() switch
                    {
                        "liquid" => "bg-info",
                        "solid" => "bg-success",
                        "gel" => "bg-warning text-dark",
                        "select type of medicine" => "bg-secondary",
                        _ => "bg-secondary"
                    }
                }).ToList();

                return Json(new { success = true, data = types });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading medicine types");
                return Json(new { success = false, message = "An error occurred while loading medicine types." });
            }
        }

        // ======= NEW: Get user accessible source types =======
        public async Task<IActionResult> GetAccessibleSourceTypes()
        {
            try
            {
                var userRole = await GetCurrentUserRoleAsync();
                var accessibleSourceTypes = await _repo.GetAccessibleSourceTypesAsync(userRole);

                return Json(new { success = true, data = accessibleSourceTypes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading accessible source types");
                return Json(new { success = false, message = "An error occurred while loading accessible source types." });
            }
        }

        // ======= UPDATED: Test database connectivity WITH ROLE-BASED ACCESS =======
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                // Get user's plant ID and role for filtering
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userRole = await GetCurrentUserRoleAsync();
                var currentUser = GetCurrentUserIdentifier();

                // Simple test to check if database is accessible WITH ROLE-BASED FILTERING
                var count = await _repo.GetTotalExpiredCountAsync(userPlantId, userRole, currentUser);
                return Json(new { success = true, message = "Database connection successful", count = count, plantId = userPlantId, userRole = userRole });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Database connection failed: " + ex.Message });
            }
        }

        // ======= Helper methods to get current user's plant ID and role =======
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
                await _auditService.LogAsync("expired_medicine", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> GetCurrentUserRoleAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                return await _repo.GetUserRoleAsync(userName);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("expired_medicine", "ROLE_ERROR", "system", null, null,
                    $"Error getting user role: {ex.Message}");
                return null;
            }
        }
        private string GetCurrentUserIdentifier()
        {
            // Use the same format as CompounderIndent.CreatedBy
            // This is typically: User.Identity.Name + " - " + User.GetFullName()
            var userName = User.Identity?.Name;
            var fullName = User.GetFullName();  // Extension method

            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(fullName))
            {
                return $"{userName} - {fullName}";
            }

            return userName ?? "unknown";
        }

    }
}