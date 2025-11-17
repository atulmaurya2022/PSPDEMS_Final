using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedExaminationApproval")]
    public class MedExaminationApprovalController : Controller
    {
        private readonly IMedExaminationApprovalRepository _approvalRepository;
        private readonly ILogger<MedExaminationApprovalController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public MedExaminationApprovalController(
            IMedExaminationApprovalRepository approvalRepository,
            ILogger<MedExaminationApprovalController> logger,
            ApplicationDbContext db,
            IAuditService auditService)
        {
            _approvalRepository = approvalRepository;
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

                return await _approvalRepository.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user plant ID");
                await _auditService.LogAsync("med_exam_approval", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

        public async Task<IActionResult> Index(int? categoryId = null, int? locationId = null, string? approvalStatus = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Medical Examination Approval Index accessed for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_approval", "INDEX_VIEW", "main", null, null,
                    $"Medical examination approval list accessed - CategoryID: {categoryId}, LocationID: {locationId}, Status: {approvalStatus ?? "All"}, Plant: {userPlantId}");

                // Show all records by default (null = all statuses)
                var model = await _approvalRepository.GetApprovalDataAsync(categoryId, locationId, approvalStatus, userPlantId);

                await _auditService.LogAsync("med_exam_approval", "INDEX_SUCCESS", "main", null, null,
                    $"Approval data loaded - Count: {model?.ApprovalItems?.Count ?? 0}, Plant: {userPlantId}");

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Medical Examination Approval Index page");
                await _auditService.LogAsync("med_exam_approval", "INDEX_FAILED", "main", null, null,
                    $"Failed to load medical examination approval index: {ex.Message}");
                return View(new MedExaminationApprovalViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> FilterData(MedExaminationApprovalViewModel model)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Filtering approval data for plant: {userPlantId}");

                await _auditService.LogAsync("med_exam_approval", "FILTER_ATTEMPT", "bulk", null, null,
                    $"Filtering approval data - CategoryID: {model.SelectedCategoryId}, LocationID: {model.SelectedLocationId}, Status: {model.SelectedApprovalStatus ?? "All"}, Plant: {userPlantId}");

                var filteredModel = await _approvalRepository.GetApprovalDataAsync(
                    model.SelectedCategoryId,
                    model.SelectedLocationId,
                    model.SelectedApprovalStatus,
                    userPlantId);

                await _auditService.LogAsync("med_exam_approval", "FILTER_SUCCESS", "bulk", null, null,
                    $"Approval data filtered - Results: {filteredModel?.ApprovalItems?.Count ?? 0}, Plant: {userPlantId}");

                return PartialView("_ApprovalGridPartial", filteredModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering approval data");
                await _auditService.LogAsync("med_exam_approval", "FILTER_FAILED", "bulk", null, null,
                    $"Filter approval data failed: {ex.Message}");
                return PartialView("_ApprovalGridPartial", new MedExaminationApprovalViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSelected([FromBody] List<int> approvalIds)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name ?? "System";

                await _auditService.LogAsync("med_exam_approval", "APPROVE_ATTEMPT", "bulk", null, null,
                    $"Bulk approval attempted - Count: {approvalIds?.Count ?? 0}, User: {userName}, Plant: {userPlantId}");

                if (!approvalIds?.Any() == true)
                {
                    await _auditService.LogAsync("med_exam_approval", "APPROVE_INVALID", "bulk", null, null,
                        "Bulk approval failed - No items selected");
                    return Json(new { success = false, message = "No items selected for approval." });
                }

                _logger.LogInformation($"Approving {approvalIds.Count} examinations for plant: {userPlantId}");

                var approvedCount = await _approvalRepository.ApproveExaminationsAsync(approvalIds, userName, userPlantId);

                _logger.LogInformation($"Successfully approved {approvedCount} examinations");

                // Log successful bulk approval (critical operation)
                await _auditService.LogUpdateAsync("med_exam_approval", "bulk",
                    null, new
                    {
                        ApprovedCount = approvedCount,
                        ApprovalIds = approvalIds,
                        ApprovedBy = userName,
                        PlantId = userPlantId
                    },
                    $"Bulk approval successful - {approvedCount} examinations approved by: {userName} in plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    message = $"Successfully approved {approvedCount} examination(s).",
                    approvedCount = approvedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving examinations");
                await _auditService.LogAsync("med_exam_approval", "APPROVE_ERROR", "bulk", null, null,
                    $"Bulk approval error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while approving examinations." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSelected([FromBody] RejectRequest request)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name ?? "System";

                await _auditService.LogAsync("med_exam_approval", "REJECT_ATTEMPT", "bulk", null, null,
                    $"Bulk rejection attempted - Count: {request?.ApprovalIds?.Count ?? 0}, Reason: {request?.RejectionReason ?? "Not provided"}, User: {userName}, Plant: {userPlantId}");

                if (!request.ApprovalIds?.Any() == true)
                {
                    await _auditService.LogAsync("med_exam_approval", "REJECT_INVALID", "bulk", null, null,
                        "Bulk rejection failed - No items selected");
                    return Json(new { success = false, message = "No items selected for rejection." });
                }

                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    await _auditService.LogAsync("med_exam_approval", "REJECT_NOREASON", "bulk", null, null,
                        "Bulk rejection validation failed - No rejection reason provided");
                    return Json(new { success = false, message = "Rejection reason is required." });
                }

                _logger.LogInformation($"Rejecting {request.ApprovalIds.Count} examinations for plant: {userPlantId}");

                var rejectedCount = await _approvalRepository.RejectExaminationsAsync(
                    request.ApprovalIds,
                    userName,
                    request.RejectionReason,
                    userPlantId);

                _logger.LogInformation($"Successfully rejected {rejectedCount} examinations");

                // Log successful bulk rejection (critical operation)
                await _auditService.LogUpdateAsync("med_exam_approval", "bulk",
                    null, new
                    {
                        RejectedCount = rejectedCount,
                        ApprovalIds = request.ApprovalIds,
                        RejectedBy = userName,
                        RejectionReason = request.RejectionReason,
                        PlantId = userPlantId
                    },
                    $"Bulk rejection successful - {rejectedCount} examinations rejected by: {userName} in plant: {userPlantId}, Reason: {request.RejectionReason}");

                return Json(new
                {
                    success = true,
                    message = $"Successfully rejected {rejectedCount} examination(s).",
                    rejectedCount = rejectedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting examinations");
                await _auditService.LogAsync("med_exam_approval", "REJECT_ERROR", "bulk", null, null,
                    $"Bulk rejection error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while rejecting examinations." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnApproveSelected([FromBody] List<int> approvalIds)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name ?? "System";

                await _auditService.LogAsync("med_exam_approval", "UNAPPROVE_ATTEMPT", "bulk", null, null,
                    $"Bulk un-approval attempted - Count: {approvalIds?.Count ?? 0}, User: {userName}, Plant: {userPlantId}");

                if (!approvalIds?.Any() == true)
                {
                    await _auditService.LogAsync("med_exam_approval", "UNAPPROVE_INVALID", "bulk", null, null,
                        "Bulk un-approval failed - No items selected");
                    return Json(new { success = false, message = "No items selected for un-approval." });
                }

                _logger.LogInformation($"Un-approving {approvalIds.Count} examinations for plant: {userPlantId}");

                var unApprovedCount = await _approvalRepository.UnApproveExaminationsAsync(approvalIds, userName, userPlantId);

                _logger.LogInformation($"Successfully un-approved {unApprovedCount} examinations");

                // Log successful bulk un-approval (critical operation)
                await _auditService.LogUpdateAsync("med_exam_approval", "bulk",
                    null, new
                    {
                        UnApprovedCount = unApprovedCount,
                        ApprovalIds = approvalIds,
                        UnApprovedBy = userName,
                        PlantId = userPlantId
                    },
                    $"Bulk un-approval successful - {unApprovedCount} examinations un-approved by: {userName} in plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    message = $"Successfully un-approved {unApprovedCount} examination(s).",
                    unApprovedCount = unApprovedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error un-approving examinations");
                await _auditService.LogAsync("med_exam_approval", "UNAPPROVE_ERROR", "bulk", null, null,
                    $"Bulk un-approval error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while un-approving examinations." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoryScheduledMonths(int categoryId)
        {
            try
            {
                await _auditService.LogAsync("med_exam_approval", "GET_CAT_SCHED", categoryId.ToString(), null, null,
                    $"Category scheduled months requested - CategoryID: {categoryId}");

                var category = await _db.Set<MedExamCategory>()
                    .FirstOrDefaultAsync(c => c.CatId == categoryId);

                if (category != null)
                {
                    await _auditService.LogAsync("med_exam_approval", "CAT_SCHED_OK", categoryId.ToString(), null, null,
                        $"Category schedule retrieved - Category: {category.CatName}, Months: {category.MonthsSched ?? "None"}");

                    return Json(new
                    {
                        success = true,
                        scheduledMonths = category.MonthsSched ?? "",
                        categoryName = category.CatName,
                        yearsFreq = category.YearsFreq,
                        annuallyRule = category.AnnuallyRule
                    });
                }
                else
                {
                    await _auditService.LogAsync("med_exam_approval", "CAT_NOTFOUND", categoryId.ToString(), null, null,
                        $"Category not found - CategoryID: {categoryId}");
                    return Json(new { success = false, message = "Category not found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting scheduled months for category: {categoryId}");
                await _auditService.LogAsync("med_exam_approval", "CAT_SCHED_ERROR", categoryId.ToString(), null, null,
                    $"Get category schedule error: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving category information." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUserPlant()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_exam_approval", "GET_PLANT", "system", null, null,
                    $"Get current user plant requested - Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("med_exam_approval", "PLANT_NOTFOUND", "system", null, null,
                        "No plant assigned to user");
                    return Json(new { success = false, message = "No plant assigned" });
                }

                var plant = await _db.org_plants.FindAsync((short)userPlantId.Value);

                await _auditService.LogAsync("med_exam_approval", "PLANT_SUCCESS", "system", null, null,
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
                await _auditService.LogAsync("med_exam_approval", "PLANT_ERROR", "system", null, null,
                    $"Get plant error: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving plant information" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetApprovalStatistics()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_exam_approval", "GET_STATS", "system", null, null,
                    $"Approval statistics requested - Plant: {userPlantId}");

                var stats = await _approvalRepository.GetApprovalStatisticsAsync(userPlantId);

                await _auditService.LogAsync("med_exam_approval", "STATS_SUCCESS", "system", null, null,
                    $"Approval statistics retrieved - Plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    statistics = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval statistics");
                await _auditService.LogAsync("med_exam_approval", "STATS_ERROR", "system", null, null,
                    $"Get statistics error: {ex.Message}");
                return Json(new { success = false, message = "Error retrieving statistics" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewUnApproved()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_exam_approval", "VIEW_UNAPPROVED", "bulk", null, null,
                    $"Viewing unapproved examinations - Plant: {userPlantId}");

                var model = await _approvalRepository.GetApprovalDataAsync(null, null, "UnApproved", userPlantId);

                await _auditService.LogAsync("med_exam_approval", "UNAPPROVED_OK", "bulk", null, null,
                    $"Unapproved examinations loaded - Count: {model?.ApprovalItems?.Count ?? 0}, Plant: {userPlantId}");

                return PartialView("_ApprovalGridPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading un-approved examinations");
                await _auditService.LogAsync("med_exam_approval", "UNAPPROVED_ERROR", "bulk", null, null,
                    $"View unapproved error: {ex.Message}");
                return PartialView("_ApprovalGridPartial", new MedExaminationApprovalViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> RefreshGrid(int? categoryId = null, int? locationId = null, string? approvalStatus = "Pending")
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("med_exam_approval", "REFRESH_GRID", "bulk", null, null,
                    $"Grid refresh requested - CategoryID: {categoryId}, LocationID: {locationId}, Status: {approvalStatus ?? "All"}, Plant: {userPlantId}");

                var model = await _approvalRepository.GetApprovalDataAsync(categoryId, locationId, approvalStatus, userPlantId);

                await _auditService.LogAsync("med_exam_approval", "REFRESH_OK", "bulk", null, null,
                    $"Grid refreshed - Count: {model?.ApprovalItems?.Count ?? 0}, Plant: {userPlantId}");

                return PartialView("_ApprovalGridPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing grid");
                await _auditService.LogAsync("med_exam_approval", "REFRESH_ERROR", "bulk", null, null,
                    $"Grid refresh error: {ex.Message}");
                return PartialView("_ApprovalGridPartial", new MedExaminationApprovalViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTestLocations()
        {
            try
            {
                await _auditService.LogAsync("med_exam_approval", "GET_LOCATIONS", "system", null, null,
                    "Test locations requested");

                var locations = await _approvalRepository.GetTestLocationsAsync();

                await _auditService.LogAsync("med_exam_approval", "LOCATIONS_OK", "system", null, null,
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
                await _auditService.LogAsync("med_exam_approval", "LOCATIONS_ERROR", "system", null, null,
                    $"Get locations error: {ex.Message}");
                return Json(new List<object>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMissingApprovalRecords()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var userName = User.Identity?.Name ?? "System";

                await _auditService.LogAsync("med_exam_approval", "CREATE_MISSING", "system", null, null,
                    $"Creating missing approval records - User: {userName}, Plant: {userPlantId}");

                // Get all examination results that don't have approval records
                var resultsWithoutApprovals = await _db.Set<MedExaminationResult>()
                    .Where(r => !_db.Set<MedExaminationApproval>().Any(a => a.ResultId == r.ResultId))
                    .ToListAsync();

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    resultsWithoutApprovals = resultsWithoutApprovals.Where(r => r.PlantId == plantIdAsShort).ToList();
                }

                await _auditService.LogAsync("med_exam_approval", "MISSING_FOUND", "system", null, null,
                    $"Found {resultsWithoutApprovals.Count} results without approval records - Plant: {userPlantId}");

                var createdCount = 0;

                foreach (var result in resultsWithoutApprovals)
                {
                    var approval = new MedExaminationApproval
                    {
                        ResultId = result.ResultId,
                        EmpUid = result.EmpUid,
                        CatId = result.CatId,
                        ApprovalStatus = "Pending",
                        PlantId = result.PlantId,
                        CreatedBy = userName,
                        CreatedOn = DateTime.Now
                    };

                    _db.Set<MedExaminationApproval>().Add(approval);
                    createdCount++;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Created {createdCount} approval records for plant {userPlantId}");

                // Log successful creation (critical operation)
                await _auditService.LogCreateAsync("med_exam_approval", "bulk",
                    new
                    {
                        CreatedCount = createdCount,
                        CreatedBy = userName,
                        PlantId = userPlantId
                    },
                    $"Missing approval records created - {createdCount} records created by: {userName} in plant: {userPlantId}");

                return Json(new
                {
                    success = true,
                    message = $"Successfully created {createdCount} approval record(s).",
                    createdCount = createdCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating missing approval records");
                await _auditService.LogAsync("med_exam_approval", "CREATE_ERROR", "system", null, null,
                    $"Create missing approval records error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating approval records." });
            }
        }
    }

    // Helper classes for request binding
    public class RejectRequest
    {
        public List<int> ApprovalIds { get; set; } = new List<int>();
        public string? RejectionReason { get; set; }
    }
}