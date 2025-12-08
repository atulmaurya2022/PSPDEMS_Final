using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace EMS.WebApp.Controllers
{
    [Authorize(Policy = "AccessCompounderIndent")]
    public class CompounderIndentController : Controller
    {
        private readonly ICompounderIndentRepository _repo;
        private readonly IStoreIndentRepository _storeRepo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public CompounderIndentController(ICompounderIndentRepository repo, IStoreIndentRepository storeRepo,
            IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _storeRepo = storeRepo;
            _cache = cache;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userRole = await GetUserRoleAsync();
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("compounder_indent", "INDEX_VIEW", "main", null, null,
                    $"Compounder indent module accessed by user role: {userRole}, Plant: {userPlantId}");
                return View();
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "INDEX_FAILED", "main", null, null,
                    $"Failed to load compounder indent index: {ex.Message}");
                throw;
            }
        }

        // Updated LoadData method with plant-wise filtering

        public async Task<IActionResult> LoadData(string indentType = null)
        {
            try
            {
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                var userPlantId = await GetCurrentUserPlantIdAsync();
                await _auditService.LogAsync("compounder*indent", "LOAD_DATA", "multiple", null, null,
                    $"Load data attempted - IndentType: {indentType ?? "all"}, User: {currentUser}, Plant: {userPlantId}");
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();

                if (isStoreUser)
                {
                    if (string.IsNullOrEmpty(indentType) || indentType != "Compounder Inventory")
                    {
                        await _auditService.LogAsync("compounder*indent", "LOAD_FILTERED", "multiple", null, null,
                            $"Store user access filtered - only Compounder Inventory allowed");
                        return Json(new { data = new List<object>() });
                    }
                    indentType = "Compounder Inventory";
                }

                IEnumerable<CompounderIndent> list;
                if (string.IsNullOrEmpty(indentType))
                {
                    //list = await _repo.ListAsync(currentUser, userPlantId);
                    list = await _repo.ListAsync(currentUser, userPlantId, isDoctor);
                }
                else if (indentType == "Compounder Inventory")
                {
                    //var approvedIndents = await _repo.ListByStatusAsync("Approved", currentUser, userPlantId);
                    //var pendingIndents = await _repo.ListByStatusAsync("Pending", currentUser, userPlantId);
                    //list = approvedIndents.Concat(pendingIndents).OrderBy(x => x.IndentDate);
                    // UPDATED: Pass isDoctor parameter to both calls
                    var approvedIndents = await _repo.ListByStatusAsync("Approved", currentUser, userPlantId, isDoctor);
                    var pendingIndents = await _repo.ListByStatusAsync("Pending", currentUser, userPlantId, isDoctor);
                    list = approvedIndents.Concat(pendingIndents).OrderBy(x => x.IndentDate);
                }
                else
                {
                    //list = await _repo.ListByTypeAsync(indentType, currentUser, userPlantId);
                    list = await _repo.ListByTypeAsync(indentType, currentUser, userPlantId, isDoctor);
                }

                var result = new List<object>();
                foreach (var x in list)
                {
                    var items = await _repo.GetItemsByIndentIdAsync(x.IndentId, userPlantId);
                    var allItemsReceived = items.Any() && items.All(item => item.PendingQuantity == 0);
                    var hasItems = items.Any();
                    // ADD THIS NEW CHECK
                    var allItemsZeroReceived = items.Any() && items.All(item => item.ReceivedQuantity == 0);
                    // Calculate total pending quantity from all medicines in this indent
                    var totalPendingQty = items.Sum(item => item.PendingQuantity);

                    var itemData = new
                    {
                        x.IndentId,
                        IndentNo = x.IndentId.ToString(),
                        IndentType = indentType == "Compounder Inventory" ? "Compounder Inventory" : x.IndentType,
                        IndentDate = x.IndentDate.ToString("dd/MM/yyyy"),
                        x.Status,
                        x.CreatedBy,
                        CreatedDate = x.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        PlantName = x.OrgPlant?.plant_name ?? "Unknown Plant",
                        TotalPendingQty = totalPendingQty, // Add total pending quantity for Issued Status column
                        CanApproveReject = isDoctor && x.Status == "Pending" && x.IndentType != "Draft Indent" && indentType != "Compounder Inventory",
                        // PASS THE NEW PARAMETER
                        CanEdit = GetCanEditPermission(indentType, x, currentUser, isDoctor, isStoreUser, isCompounderUser, hasItems, allItemsReceived, allItemsZeroReceived),
                        CanDelete = GetCanDeletePermission(indentType, x, currentUser, isDoctor, isStoreUser, isCompounderUser, allItemsReceived, allItemsZeroReceived),
                        IsDoctor = isDoctor,
                        IsStoreUser = isStoreUser,
                        IsCompounderUser = isCompounderUser,
                        AllItemsReceived = allItemsReceived,
                        HasItems = hasItems
                    };
                    result.Add(itemData);
                }

                await _auditService.LogAsync("compounder*indent", "LOAD_SUCCESS", "multiple", null, null,
                    $"Data loaded successfully - Count: {result.Count}, IndentType: {indentType ?? "all"}, Plant: {userPlantId}");
                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder*indent", "LOAD_FAILED", "multiple", null, null,
                    $"Failed to load data: {ex.Message}");
                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetAvailableStoreBatchesForMedicine(int medItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("compounder_indent", "GET_STORE_BATCHES", medItemId.ToString(), null, null,
                    $"Store batches requested for medicine: {medItemId}, Plant: {userPlantId}");

                var batches = await _repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, userPlantId);

                await _auditService.LogAsync("compounder_indent", "GET_STORE_BATCH_OK", medItemId.ToString(), null, null,
                    $"Store batches loaded - Count: {batches.Count()}, Plant: {userPlantId}");

                return Json(new { success = true, data = batches });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "GET_STORE_BATCH_ERR", medItemId.ToString(), null, null,
                    $"Get store batches error: {ex.Message}");
                return Json(new { success = false, data = new List<object>(), message = "Error loading store batches." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTotalReceivedFromStore(int medItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                
                // CRITICAL: Check which repository and method we're actually calling
                var totalStock = await _repo.GetTotalAvailableStockFromStoreAsync(medItemId, userPlantId);

                Console.WriteLine($"Repository returned: {totalStock}");

                // COMPARISON: Also test direct SQL to see the difference
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var sqlResult = userPlantId.HasValue ? await dbContext.Database
                    .SqlQueryRaw<int>(@"
                        SELECT ISNULL(SUM(sib.available_stock), 0)
                        FROM store_indent si
                        JOIN store_indent_item sii ON si.indent_id = sii.indent_id
                        JOIN store_indent_batch sib ON sii.indent_item_id = sib.indent_item_id
                        WHERE si.status = 'Approved' AND sii.med_item_id = {0} AND si.plant_id = {1}",
                        medItemId, userPlantId.Value)
                    .FirstOrDefaultAsync() : 0;

                await _auditService.LogAsync("compounder_indent", "GET_STORE_STOCK_OK", medItemId.ToString(), null, null,
                    $"Store available stock retrieved: {totalStock}, Plant: {userPlantId}, SQL: {sqlResult}");

                return Json(new { success = true, data = totalStock });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                await _auditService.LogAsync("compounder_indent", "GET_STORE_STOCK_ERR", medItemId.ToString(), null, null,
                    $"Get store stock error: {ex.Message}");
                return Json(new { success = false, data = 0, message = "Error loading store stock." });
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
                await _auditService.LogAsync("compounder_indent", "PLANT_ERROR", "system", null, null,
                    $"Error getting user plant: {ex.Message}");
                return null;
            }
        }

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
                await _auditService.LogAsync("compounder_indent", "ROLE_ERROR", "system", null, null,
                    $"Error getting user role: {ex.Message}");
                return null;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUserRole()
        {
            try
            {
                var userRole = await GetUserRoleAsync();
                var userPlantId = await GetCurrentUserPlantIdAsync();
                await _auditService.LogAsync("compounder_indent", "GET_ROLE", "system", null, null,
                    $"User role requested: {userRole ?? "null"}, Plant: {userPlantId}");
                return Json(new { success = true, role = userRole });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "GET_ROLE_FAIL", "system", null, null,
                    $"Get user role API failed: {ex.Message}");
                return Json(new { success = false, role = string.Empty });
            }
        }

        // Method to check if user has store role
        private async Task<bool> IsStoreUserAsync()
        {
            try
            {
                var userRole = await GetUserRoleAsync();
                return !string.IsNullOrEmpty(userRole) && userRole.ToLower().Contains("store");
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "CHECK_STORE_ERR", "system", null, null,
                    $"Error checking store role: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> IsCompounderUserAsync()
        {
            try
            {
                var userRole = await GetUserRoleAsync();
                return !string.IsNullOrEmpty(userRole) && userRole.ToLower().Contains("compounder");
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "CHECK_COMP_ERR", "system", null, null,
                    $"Error checking compounder role: {ex.Message}");
                return false;
            }
        }

        private bool GetCanEditPermission(string indentType, CompounderIndent indent, string currentUser,
    bool isDoctor, bool isStoreUser, bool isCompounderUser, bool hasItems, bool allItemsReceived, bool allItemsZeroReceived)
        {
            if (isDoctor)
                return false;

            if (indentType == "Compounder Inventory")
            {
                if (isStoreUser)
                    return hasItems;

                if (isCompounderUser)
                    return false;

                return hasItems;
            }
            else
            {
                if (isStoreUser)
                    return false;

                if (isCompounderUser)
                {
                    // For pending indents with fully received items, don't allow edit
                    if (indent.Status == "Pending" && allItemsReceived)
                        return false;

                    // For pending indents with zero received items, allow edit if creator
                    if (indent.Status == "Pending" && allItemsZeroReceived)
                        return indent.CreatedBy == currentUser;

                    // For draft indents - only creator can edit
                    if (indent.IndentType == "Draft Indent")
                        return indent.CreatedBy == currentUser;

                    // For pending indents with partial receipt - don't allow edit
                    else if (indent.Status == "Pending")
                        return false;

                    else
                        return indent.Status == "Pending" || string.IsNullOrEmpty(indent.Status);
                }

                // For non-compounder users
                // For pending indents with fully received items, don't allow edit
                if (indent.Status == "Pending" && allItemsReceived)
                    return false;

                // For pending indents with zero received items, allow edit if creator
                if (indent.Status == "Pending" && allItemsZeroReceived)
                    return indent.CreatedBy == currentUser;

                // For draft indents - only creator can edit
                if (indent.IndentType == "Draft Indent")
                    return indent.CreatedBy == currentUser;

                // For pending indents with partial receipt - don't allow edit
                else if (indent.Status == "Pending")
                    return false;

                else
                    return indent.Status == "Pending" || string.IsNullOrEmpty(indent.Status);
            }
        }
        private bool GetCanDeletePermission(string indentType, CompounderIndent indent, string currentUser,
    bool isDoctor, bool isStoreUser, bool isCompounderUser, bool allItemsReceived, bool allItemsZeroReceived)
        {
            if (isDoctor)
                return false;

            if (isStoreUser)
                return false;

            if (indentType == "Compounder Inventory")
                return false;

            if (isCompounderUser)
            {
                // For pending indents with fully received items, don't allow delete
                if (indent.Status == "Pending" && allItemsReceived)
                    return false;

                // For pending indents with zero received items, allow delete if creator
                if (indent.Status == "Pending" && allItemsZeroReceived)
                    return indent.CreatedBy == currentUser;

                // For draft indents - only creator can delete
                if (indent.IndentType == "Draft Indent")
                    return indent.CreatedBy == currentUser;

                // For pending indents with partial receipt - don't allow delete
                else if (indent.Status == "Pending")
                    return false;

                else
                    return indent.Status == "Pending" || string.IsNullOrEmpty(indent.Status);
            }

            // For non-compounder users
            // For pending indents with fully received items, don't allow delete
            if (indent.Status == "Pending" && allItemsReceived)
                return false;

            // For pending indents with zero received items, allow delete if creator
            if (indent.Status == "Pending" && allItemsZeroReceived)
                return indent.CreatedBy == currentUser;

            // For draft indents - only creator can delete
            if (indent.IndentType == "Draft Indent")
                return indent.CreatedBy == currentUser;

            // For pending indents with partial receipt - don't allow delete
            else if (indent.Status == "Pending")
                return false;

            else
                return indent.Status == "Pending" || string.IsNullOrEmpty(indent.Status);
        }
        public async Task<IActionResult> ApproveReject(int id, string indentType = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("compounder_indent", "APPROVE_VIEW", id.ToString(), null, null,
                    $"Approve/reject modal accessed for indent: {id}, type: {indentType}, Plant: {userPlantId}");

                var item = await _repo.GetByIdWithItemsAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("compounder_indent", "APPROVE_NOTFND", id.ToString(), null, null,
                        $"Indent not found for approve/reject: {id} or access denied for plant: {userPlantId}");
                    return NotFound();
                }

                foreach (var medicine in item.CompounderIndentItems)
                {
                    medicine.TotalReceivedFromStore = await _repo.GetTotalAvailableStockFromStoreAsync(medicine.MedItemId, userPlantId);
                }

                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("compounder_indent", "APPROVE_DENIED", id.ToString(), null, null,
                        $"Approve/reject access denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
                }

                if (item.Status != "Pending")
                {
                    await _auditService.LogAsync("compounder_indent", "APPROVE_INVALID", id.ToString(), null, null,
                        $"Invalid status for approval: {item.Status}");
                    return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
                }

                await PopulateMedicineDropdownAsync();
                ViewBag.IsCompounderInventoryMode = indentType == "Compounder Inventory";

                await _auditService.LogAsync("compounder_indent", "APPROVE_ACCESS", id.ToString(), null, null,
                    $"Approve/reject modal accessed successfully - Status: {item.Status}, Plant: {item.OrgPlant?.plant_name}");

                return PartialView("_ApproveRejectModal", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "APPROVE_ERROR", id.ToString(), null, null,
                    $"Error accessing approve/reject modal: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int indentId, string status, string comments)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("compounder_indent", "UPDATE_STATUS", indentId.ToString(), null, null,
                    $"Status update attempted - Status: {status}, IndentId: {indentId}, Plant: {userPlantId}");

                comments = SanitizeString(comments);
                status = SanitizeString(status);

                if (!IsCommentsSecure(comments))
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_SECURITY", indentId.ToString(), null, null,
                        "Status update blocked - insecure comments detected");
                    return Json(new { success = false, message = "Invalid input detected in comments. Please remove any script tags or unsafe characters." });
                }

                if (string.IsNullOrWhiteSpace(comments) || comments.Length < 2)
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_INVALID", indentId.ToString(), null, null,
                        "Status update validation failed - insufficient comments");
                    return Json(new { success = false, message = "Please provide detailed comments (minimum 2 characters)." });
                }

                if (comments.Length > 500)
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_TOOLONG", indentId.ToString(), null, null,
                        "Status update validation failed - comments too long");
                    return Json(new { success = false, message = "Comments cannot exceed 500 characters." });
                }

                if (status != "Approved" && status != "Rejected")
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_BADSTAT", indentId.ToString(), null, null,
                        $"Status update validation failed - invalid status: {status}");
                    return Json(new { success = false, message = "Invalid status." });
                }

                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_DENIED", indentId.ToString(), null, null,
                        $"Status update denied for role: {userRole}");
                    return Json(new { success = false, message = "Access denied. Only doctors can approve/reject indents." });
                }

                var indent = await _repo.GetByIdAsync(indentId, userPlantId);
                if (indent == null)
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_NOTFND", indentId.ToString(), null, null,
                        "Status update failed - indent not found or access denied");
                    return Json(new { success = false, message = "Compounder Indent not found or access denied." });
                }

                if (indent.Status != "Pending")
                {
                    await _auditService.LogAsync("compounder_indent", "UPDATE_WRONGST", indentId.ToString(), null, null,
                        $"Status update failed - current status: {indent.Status}");
                    return Json(new { success = false, message = "Only pending indents can be approved or rejected." });
                }

                var oldIndent = new { Status = indent.Status, IndentType = indent.IndentType, Comments = indent.Comments };

                indent.Status = status;
                indent.Comments = comments;
                indent.ApprovedBy = User.Identity?.Name + " - " + User.GetFullName();
                indent.ApprovedDate = DateTime.Now;
                indent.IndentType = status == "Approved" ? "Approved Indents" : "Rejected Indents";

                await _repo.UpdateAsync(indent);

                await _auditService.LogAsync("compounder_indent", "UPDATE_SUCCESS", indentId.ToString(),
                    oldIndent, new { Status = status, IndentType = indent.IndentType, Comments = comments },
                    $"Indent {status.ToLower()} successfully by: {User.Identity?.Name}, Plant: {indent.OrgPlant?.plant_name}");

                return Json(new { success = true, message = $"Compounder Indent {status.ToLower()} successfully." });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "UPDATE_ERROR", indentId.ToString(), null, null,
                    $"Status update failed with error: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the status." });
            }
        }


        public async Task<IActionResult> Create()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("compounder_indent", "CREATE_FORM", "new", null, null,
                    $"Create form accessed for plant: {userPlantId}");

                var isStoreUser = await IsStoreUserAsync();

                if (isStoreUser)
                {
                    await _auditService.LogAsync("compounder_indent", "CREATE_DENIED", "new", null, null,
                        "Store user create access denied");
                    return Json(new { success = false, message = "Access denied. Store users can only manage Compounder Inventory." });
                }

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync();

                var model = new CompounderIndent
                {
                    IndentDate = DateTime.Today,
                    IndentType = "Pending Indents",
                    Status = "Pending",
                    plant_id = (short)(userPlantId ?? 1)
                };

                await _auditService.LogAsync("compounder_indent", "CREATE_OK", "new", null, null,
                    $"Create form loaded successfully for plant: {userPlantId}");

                return PartialView("_CreateEdit", model);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "CREATE_ERROR", "new", null, null,
                    $"Create form error: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(CompounderIndent model, string medicinesJson, string actionType = "submit")
        {
            string recordId = "new";
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                recordId = model.IndentId.ToString();

                if (!userPlantId.HasValue)
                {
                    await _auditService.LogAsync("compounder_indent", "CREATE_NO_PLANT", recordId, null, model,
                        "Create failed - user has no plant assigned");
                    ViewBag.Error = "User is not assigned to any plant. Please contact administrator.";
                    await PopulateMedicineDropdownAsync();
                    await PopulateIndentTypeDropdownAsync();
                    return PartialView("_CreateEdit", model);
                }

                model.plant_id = (short)userPlantId.Value;

                await _auditService.LogAsync("compounder_indent", "CREATE_ATTEMPT", recordId, null, model,
                    $"Indent creation attempted - ActionType: {actionType}, IndentType: {model.IndentType}, Plant: {model.plant_id}");

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync();

                model = SanitizeInput(model);

                if (!IsInputSecure(model))
                {
                    await _auditService.LogAsync("compounder_indent", "CREATE_SECURITY", recordId, null, model,
                        "Create blocked - insecure input detected");
                    ViewBag.Error = "Invalid input detected. Please remove any script tags or unsafe characters.";
                    return PartialView("_CreateEdit", model);
                }

                if (actionType.ToLower() == "save")
                {
                    model.IndentType = "Draft Indent";
                    model.Status = "Draft";
                }
                else
                {
                    if (model.IndentType == "Draft Indent")
                    {
                        model.IndentType = "Pending Indents";
                        model.Status = "Pending";
                    }
                    else
                    {
                        SetStatusBasedOnIndentType(model);
                    }
                }

                ModelState.Remove("plant_id");

                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("compounder_indent", "CREATE_INVALID", recordId, null, model,
                        $"Create validation failed: {validationErrors}");

                    ViewBag.Error = "Please check the form for validation errors.";
                    return PartialView("_CreateEdit", model);
                }

                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_compounderindent_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    await _auditService.LogAsync("compounder_indent", "CREATE_RATELIMIT", recordId, null, model,
                        $"Create rate limited - {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 Compounder Indents every 5 minutes. Please wait.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                model.CreatedBy = User.Identity?.Name + " - " + User.GetFullName();
                model.CreatedDate = DateTime.Now;
                await _repo.AddAsync(model);
                recordId = model.IndentId.ToString();

                var medicineValidationResult = await ProcessMedicinesForCreateEnhanced(model.IndentId, medicinesJson, userPlantId);
                if (!medicineValidationResult.Success)
                {
                    await _auditService.LogAsync("compounder_indent", "CREATE_MED_FAIL", recordId, null, model,
                        $"Medicine processing failed: {medicineValidationResult.ErrorMessage}");
                    ViewBag.Error = medicineValidationResult.ErrorMessage;
                    return PartialView("_CreateEdit", model);
                }

                await _auditService.LogAsync("compounder_indent", "CREATE_SUCCESS", recordId, null, model,
                    $"Indent created successfully - ActionType: {actionType}, HasMedicines: {medicineValidationResult.HasMedicines}, Plant: {model.plant_id}");

                if (actionType.ToLower() == "save")
                {
                    if (medicineValidationResult.HasMedicines)
                    {
                        return Json(new { success = true, message = "Compounder Indent saved as draft successfully with medicines!", indentId = model.IndentId, actionType = "save" });
                    }
                    else
                    {
                        return Json(new { success = true, message = "Compounder Indent saved as draft successfully! You can continue editing or submit when ready.", indentId = model.IndentId, actionType = "save" });
                    }
                }
                else
                {
                    if (medicineValidationResult.HasMedicines)
                    {
                        ViewBag.Success = "Compounder Indent submitted successfully with medicines!";
                    }
                    else
                    {
                        ViewBag.Success = "Compounder Indent submitted successfully! You can now add medicines.";
                    }

                    return Json(new { success = true, redirectToEdit = !medicineValidationResult.HasMedicines, indentId = model.IndentId });
                }
            }
            catch (Exception ex)
            {
                var detailedError = $"Create failed: {ex.Message}";
                if (ex.InnerException != null)
                {
                    detailedError += $" Inner: {ex.InnerException.Message}";
                }

                await _auditService.LogAsync("compounder_indent", "CREATE_FAILED", recordId, null, model,
                    detailedError);

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
                    ViewBag.Error = $"An error occurred while creating the indent: {ex.Message}";
                }

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync();
                return PartialView("_CreateEdit", model);
            }
        }
        public async Task<IActionResult> Edit(int id, string indentType = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                // Log edit form access attempt
                await _auditService.LogAsync("compounder_indent", "EDIT_FORM", id.ToString(), null, null,
                    $"Edit form accessed - IndentType: {indentType}, Plant: {userPlantId}");

                var item = await _repo.GetByIdWithItemsAsync(id, userPlantId);
                if (item == null)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_NOTFOUND", id.ToString(), null, null,
                        $"Edit attempted on non-existent indent or unauthorized access for plant: {userPlantId}");
                    return NotFound();
                }

                // Populate TotalReceivedFromStore for each medicine item with plant filtering
                foreach (var medicine in item.CompounderIndentItems)
                {
                    medicine.TotalReceivedFromStore = await _repo.GetTotalAvailableStockFromStoreAsync(medicine.MedItemId, userPlantId);
                }

                // Get user roles
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();

                // Security check: Doctors cannot edit indents - they can only review
                if (isDoctor)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_DOC_DENY", id.ToString(), null, null,
                        "Edit denied - doctors can only review");
                    return Json(new { success = false, message = "Access denied. Doctors can only review indents, not edit them." });
                }

                // Security check: Store users can only edit Compounder Inventory
                if (isStoreUser && indentType != "Compounder Inventory")
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_STORE_DENY", id.ToString(), null, null,
                        $"Store user edit denied - wrong type: {indentType}");
                    return Json(new { success = false, message = "Access denied. Store users can only edit Compounder Inventory." });
                }

                // Security check: Compounder users CANNOT edit Compounder Inventory
                if (isCompounderUser && indentType == "Compounder Inventory")
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_COMP_DENY", id.ToString(), null, null,
                        "Compounder user denied - cannot edit inventory");
                    return Json(new { success = false, message = "Access denied. Compounder users cannot edit Compounder Inventory." });
                }

                // UPDATED: Security check for creator-only access on pending and draft indents
                // BUT exclude Compounder Inventory mode for store users
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                var isInventoryMode = indentType == "Compounder Inventory";
                var isStoreUserForInventory = isInventoryMode && isStoreUser;

                // Apply creator-only restriction ONLY if:
                // 1. It's a draft or pending indent AND
                // 2. It's NOT Compounder Inventory mode with store user AND  
                // 3. User is not the creator
                if ((item.IndentType == "Draft Indent" || item.Status == "Pending") &&
                    !isStoreUserForInventory &&
                    item.CreatedBy != currentUser)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_CREATOR_DENY", id.ToString(), null, null,
                        $"Edit denied - not creator: {currentUser} vs {item.CreatedBy}, Status: {item.Status}, Type: {item.IndentType}, IsInventory: {isInventoryMode}, IsStoreUser: {isStoreUser}");
                    return Json(new { success = false, message = "Access denied. You can only edit your own draft and pending indents." });
                }

                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync(item.CreatedBy);

                // Set ViewBag flag for Compounder Inventory mode
                ViewBag.IsCompounderInventoryMode = indentType == "Compounder Inventory";

                // Log successful edit form access
                await _auditService.LogAsync("compounder_indent", "EDIT_FORM_OK", id.ToString(), null, null,
                    $"Edit form accessed successfully - Status: {item.Status}, Type: {item.IndentType}, Plant: {item.OrgPlant?.plant_name}, IsInventoryForStore: {isStoreUserForInventory}");

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "EDIT_FORM_ERR", id.ToString(), null, null,
                    $"Edit form error: {ex.Message}");
                throw;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(CompounderIndent model, string medicinesJson, string actionType = "submit")
        {
            var recordId = model.IndentId.ToString();
            CompounderIndent oldIndent = null;

            try
            {
                // Get existing entity for audit comparison
                oldIndent = await _repo.GetByIdAsync(model.IndentId);
                if (oldIndent == null)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_NOTFOUND", recordId, null, model,
                        "Edit attempted on non-existent indent");
                    return NotFound();
                }

                // Log edit attempt (critical operation)
                await _auditService.LogAsync("compounder_indent", "EDIT_ATTEMPT", recordId, oldIndent, model,
                    $"Indent edit attempted - ActionType: {actionType}");

                // Security check: Doctors cannot edit indents - they can only review
                var userRole = await GetUserRoleAsync();
                if (userRole?.ToLower() == "doctor")
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_DOC_DENY", recordId, oldIndent, model,
                        "Edit denied - doctors can only review");
                    return Json(new { success = false, message = "Access denied. Doctors can only review indents, not edit them." });
                }
                await PopulateMedicineDropdownAsync();
                await PopulateIndentTypeDropdownAsync(model.CreatedBy);
                // Sanitize input before processing
                model = SanitizeInput(model);
                // Additional security validation
                if (!IsInputSecure(model))
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_SECURITY", recordId, oldIndent, model,
                        "Edit blocked - insecure input detected");
                    ViewBag.Error = "Invalid input detected. Please remove any script tags or unsafe characters.";
                    return PartialView("_CreateEdit", model);
                }
                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    await _auditService.LogAsync("compounder_indent", "EDIT_INVALID", recordId, oldIndent, model,
                        $"Edit validation failed: {validationErrors}");
                    ViewBag.Error = "Please check the form for validation errors.";
                    return PartialView("_CreateEdit", model);
                }
                // Rate limiting logic for edits
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_compounderindent_{userId}";
                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });
                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));
                if (timestamps.Count >= 10)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_RATELIMIT", recordId, oldIndent, model,
                        $"Edit rate limited - {timestamps.Count} attempts in 5 minutes");
                    ViewBag.Error = "⚠ You can only edit 10 Compounder Indents every 5 minutes. Please wait.";
                    return PartialView("_CreateEdit", model);
                }
                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));
                // Get existing entity to preserve creation info
                var existingIndent = await _repo.GetByIdAsync(model.IndentId);
                if (existingIndent == null)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_GONE", recordId, oldIndent, model,
                        "Edit failed - indent no longer exists");
                    ViewBag.Error = "Compounder Indent not found.";
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
                // Keep original CreatedBy and CreatedDate (don't update these)

                await _repo.UpdateAsync(existingIndent);

                // ENHANCED: Process medicines with enhanced validation
                var medicineValidationResult = await ProcessMedicinesForEditEnhanced(model.IndentId, medicinesJson);
                if (!medicineValidationResult.Success)
                {
                    await _auditService.LogAsync("compounder_indent", "EDIT_MED_FAIL", recordId, oldIndent, model,
                        $"Medicine processing failed: {medicineValidationResult.ErrorMessage}");
                    ViewBag.Error = medicineValidationResult.ErrorMessage;
                    return PartialView("_CreateEdit", model);
                }

                // Log successful edit (critical operation)
                await _auditService.LogAsync("compounder_indent", "EDIT_SUCCESS", recordId, oldIndent, existingIndent,
                    $"Indent updated successfully - ActionType: {actionType}");

                if (actionType.ToLower() == "save")
                {
                    // For save, return JSON response to keep modal open
                    return Json(new { success = true, message = "Compounder Indent saved successfully! You can continue editing or submit when ready.", actionType = "save" });
                }
                else
                {
                    // For submit, close the modal
                    ViewBag.Success = "Compounder Indent updated successfully!";
                    return Json(new { success = true });
                }
            }
            catch (Exception ex)
            {
                // Log edit failure
                await _auditService.LogAsync("compounder_indent", "EDIT_FAILED", recordId, oldIndent, model,
                    $"Indent edit failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("constraint") == true)
                {
                    ViewBag.Error = "A constraint violation occurred. Please check your input.";
                }
                else
                {
                    // Log the actual error for debugging
                    ViewBag.Error = $"An error occurred while updating the indent: {ex.Message}";
                }
                return PartialView("_CreateEdit", model);
            }
        }

        // Add individual medicine item with audit logging and ENHANCED stock validation
        [HttpPost]
        public async Task<IActionResult> AddMedicineItem(int indentId, int medItemId, string vendorCode, int raisedQuantity, int receivedQuantity = 0)
        {
            try
            {
                // Log medicine add attempt
                await _auditService.LogAsync("compounder_indent", "ADD_MEDICINE", indentId.ToString(), null, null,
                    $"Medicine add attempted - MedItemId: {medItemId}, VendorCode: {vendorCode}, RaisedQty: {raisedQuantity}");

                // Get user roles
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();

                // Get the indent to check permissions
                var indent = await _repo.GetByIdAsync(indentId);
                if (indent == null)
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_NOTFND", indentId.ToString(), null, null,
                        "Medicine add failed - indent not found");
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }

                // MODIFIED: Check if this is Compounder Inventory mode (both approved and pending)
                var isCompounderInventory = (indent?.Status == "Approved" || indent?.Status == "Pending");

                // Security check: Compounder users cannot add medicines to Compounder Inventory
                if (isCompounderUser && isCompounderInventory)
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_DENY", indentId.ToString(), null, null,
                        "Medicine add denied - compounder user cannot add to inventory");
                    return Json(new { success = false, message = "Access denied. Compounder users cannot add medicines to Compounder Inventory." });
                }

                //  Validate raised quantity against available stock in store
                var totalStoreStock = await _storeRepo.GetTotalAvailableStockFromStoreAsync(medItemId);
                if (raisedQuantity > totalStoreStock)
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_STOCK", indentId.ToString(), null, null,
                        $"Medicine add denied - raised qty ({raisedQuantity}) exceeds store stock ({totalStoreStock})");
                    return Json(new
                    {
                        success = false,
                        message = $"Raised quantity ({raisedQuantity}) cannot exceed available stock in store ({totalStoreStock})."
                    });
                }

                // Additional validation for zero or negative quantities
                if (raisedQuantity <= 0)
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_QTY_INV", indentId.ToString(), null, null,
                        "Medicine add denied - invalid raised quantity");
                    return Json(new { success = false, message = "Raised quantity must be greater than 0." });
                }

                if (totalStoreStock <= 0)
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_NO_STOCK", indentId.ToString(), null, null,
                        $"Medicine add denied - no stock available for medItemId: {medItemId}");
                    return Json(new
                    {
                        success = false,
                        message = "Cannot add this medicine as there is no available stock in store."
                    });
                }

                // Sanitize vendor code input
                vendorCode = SanitizeString(vendorCode);

                if (!string.IsNullOrEmpty(vendorCode) && !IsVendorCodeSecure(vendorCode))
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_INSECURE", indentId.ToString(), null, null,
                        $"Medicine add blocked - insecure vendor code: {vendorCode}");
                    return Json(new { success = false, message = "Invalid vendor code format. Please remove any unsafe characters." });
                }
                var currentUser = User.Identity?.Name;

                // MODIFIED: Allow modification for both pending and approved status in Compounder Inventory, or for doctors
                if (!isCompounderInventory && indent.Status != "Pending" && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_STATUS", indentId.ToString(), null, null,
                        $"Medicine add denied - wrong status: {indent.Status}");
                    return Json(new { success = false, message = "Only pending indents can be modified, or doctors can modify during approval." });
                }
                if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medItemId))
                {
                    await _auditService.LogAsync("compounder_indent", "ADD_MED_DUP", indentId.ToString(), null, null,
                        $"Medicine add failed - duplicate: {medItemId}");
                    return Json(new { success = false, message = "This medicine is already added to this indent." });
                }
                // Create new medicine item
                var newItem = new CompounderIndentItem
                {
                    IndentId = indentId,
                    MedItemId = medItemId,
                    VendorCode = vendorCode ?? string.Empty, // Allow empty vendor codes
                    RaisedQuantity = raisedQuantity,
                    ReceivedQuantity = receivedQuantity
                };

                await _repo.AddItemAsync(newItem);
                await _auditService.LogAsync("compounder_indent", "ADD_MED_OK", newItem.IndentItemId.ToString(),
                    null, newItem, $"Medicine item added successfully - Medicine: {medItemId}, Available Stock: {totalStoreStock}");

                return Json(new
                {
                    success = true,
                    message = "Medicine item added successfully!",
                    itemId = newItem.IndentItemId
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "ADD_MED_FAIL", indentId.ToString(), null, null,
                    $"Medicine add failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while adding the medicine item." });
            }
        }

        //UpdateMedicineItem with audit logging and ENHANCED stock validation
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineItem(int indentItemId, int? medItemId = null, string vendorCode = null,
            int? raisedQuantity = null, int? receivedQuantity = null, decimal? unitPrice = null,
            string batchNo = null, DateTime? expiryDate = null, int? availableStock = null)
        {
            try
            {
                // Log medicine update attempt
                await _auditService.LogAsync("compounder_indent", "UPD_MEDICINE", indentItemId.ToString(), null, null,
                    $"Medicine update attempted - ItemId: {indentItemId}");

                // Get user roles
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();
                var existingItem = await _repo.GetItemByIdAsync(indentItemId);
                if (existingItem == null)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_MED_NOTFND", indentItemId.ToString(), null, null,
                        "Medicine update failed - item not found");
                    return Json(new { success = false, message = "Medicine item not found." });
                }
                var indent = await _repo.GetByIdAsync(existingItem.IndentId);
                // MODIFIED: Check if this is Compounder Inventory mode (both approved and pending)
                var isCompounderInventory = (indent?.Status == "Approved" || indent?.Status == "Pending");

                // Security check: Compounder users cannot edit Compounder Inventory items
                if (isCompounderUser && isCompounderInventory)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_MED_DENY", indentItemId.ToString(), existingItem, null,
                        "Medicine update denied - compounder user cannot edit inventory");
                    return Json(new { success = false, message = "Access denied. Compounder users cannot edit Compounder Inventory items." });
                }

                // NEW: Validate raised quantity against available stock in store if raisedQuantity is being updated
                if (raisedQuantity.HasValue)
                {
                    var medId = medItemId ?? existingItem.MedItemId;
                    var totalStoreStock = await _storeRepo.GetTotalAvailableStockFromStoreAsync(medId);

                    if (raisedQuantity.Value > totalStoreStock)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_STOCK", indentItemId.ToString(), existingItem, null,
                            $"Medicine update denied - raised qty ({raisedQuantity.Value}) exceeds store stock ({totalStoreStock})");
                        return Json(new
                        {
                            success = false,
                            message = $"Raised quantity ({raisedQuantity.Value}) cannot exceed available stock in store ({totalStoreStock})."
                        });
                    }

                    if (raisedQuantity.Value <= 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_QTYINV", indentItemId.ToString(), existingItem, null,
                            "Medicine update blocked - invalid raised quantity");
                        return Json(new { success = false, message = "Raised quantity must be greater than 0." });
                    }

                    if (totalStoreStock <= 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_NO_STOCK", indentItemId.ToString(), existingItem, null,
                            $"Medicine update denied - no stock available for medItemId: {medId}");
                        return Json(new
                        {
                            success = false,
                            message = "Cannot update raised quantity as there is no available stock in store for this medicine."
                        });
                    }
                }

                // Sanitize string inputs
                if (vendorCode != null)
                {
                    vendorCode = SanitizeString(vendorCode);
                    if (!IsVendorCodeSecure(vendorCode))
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_INSEC", indentItemId.ToString(), existingItem, null,
                            $"Medicine update blocked - insecure vendor code: {vendorCode}");
                        return Json(new { success = false, message = "Invalid vendor code format. Please remove any unsafe characters." });
                    }
                }

                if (batchNo != null)
                {
                    batchNo = SanitizeString(batchNo);
                    if (!IsBatchNoSecure(batchNo))
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_BATCH", indentItemId.ToString(), existingItem, null,
                            $"Medicine update blocked - insecure batch: {batchNo}");
                        return Json(new { success = false, message = "Invalid batch number format. Please remove any unsafe characters." });
                    }
                }
                //Comprehensive expiry date validation
                if (expiryDate.HasValue)
                {
                    var expiryValidation = ValidateExpiryDate(expiryDate);
                    if (!expiryValidation.IsValid)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_EXPIRY", indentItemId.ToString(), existingItem, null,
                            $"Medicine update blocked - invalid expiry: {expiryValidation.ErrorMessage}");
                        return Json(new { success = false, message = expiryValidation.ErrorMessage });
                    }
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
                    existingItem.AvailableStock
                };

                // Update fields if provided
                if (medItemId.HasValue)
                    existingItem.MedItemId = medItemId.Value;

                if (!string.IsNullOrEmpty(vendorCode))
                    existingItem.VendorCode = vendorCode;

                if (raisedQuantity.HasValue)
                {
                    existingItem.RaisedQuantity = raisedQuantity.Value;
                }

                if (receivedQuantity.HasValue)
                {
                    if (receivedQuantity.Value < 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_RCVNEG", indentItemId.ToString(), existingItem, null,
                            "Medicine update blocked - negative received quantity");
                        return Json(new { success = false, message = "Received quantity cannot be negative." });
                    }

                    if (receivedQuantity.Value > existingItem.RaisedQuantity)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_MED_EXCEED", indentItemId.ToString(), existingItem, null,
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

                // Update batch number, expiry date, and available stock only if in Compounder Inventory mode
                if (isCompounderInventory)
                {
                    if (batchNo != null)
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

                    if (availableStock.HasValue)
                    {
                        if (availableStock.Value < 0)
                        {
                            await _auditService.LogAsync("compounder_indent", "UPD_MED_STKNG", indentItemId.ToString(), existingItem, null,
                                "Medicine update blocked - negative available stock");
                            return Json(new { success = false, message = "Available stock cannot be negative." });
                        }
                        existingItem.AvailableStock = availableStock.Value;
                    }
                    else if (Request.Form.ContainsKey("availableStock"))
                    {
                        // If availableStock parameter exists but is null/empty, clear the field
                        existingItem.AvailableStock = null;
                    }
                }

                await _repo.UpdateItemAsync(existingItem);

                // Log successful medicine update
                await _auditService.LogAsync("compounder_indent", "UPD_MED_OK", indentItemId.ToString(), oldItem, existingItem,
                    "Medicine item updated successfully");

                // Prepare response with expiry date warnings if applicable
                var responseMessage = "Medicine item updated successfully!";
                var warningMessage = "";

                if (existingItem.ExpiryDate.HasValue)
                {
                    var daysToExpiry = (existingItem.ExpiryDate.Value.Date - DateTime.Today).Days;
                    if (daysToExpiry <= 30 && daysToExpiry >= 0)
                    {
                        warningMessage = $"Warning: This medicine expires in {daysToExpiry} days.";
                    }
                    else if (daysToExpiry < 0)
                    {
                        warningMessage = $"Warning: This medicine expired {Math.Abs(daysToExpiry)} days ago.";
                    }
                }

                // Return updated values for UI refresh
                return Json(new
                {
                    success = true,
                    message = responseMessage,
                    warning = warningMessage,
                    data = new
                    {
                        receivedQuantity = existingItem.ReceivedQuantity,
                        pendingQuantity = existingItem.PendingQuantity,
                        unitPrice = existingItem.UnitPrice,
                        totalAmount = existingItem.TotalAmount,
                        batchNo = existingItem.BatchNo,
                        expiryDate = existingItem.ExpiryDate?.ToString("yyyy-MM-dd"),
                        availableStock = existingItem.AvailableStock,
                        daysToExpiry = existingItem.ExpiryDate.HasValue ?
                            (existingItem.ExpiryDate.Value.Date - DateTime.Today).Days : (int?)null
                    }
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "UPD_MED_FAIL", indentItemId.ToString(), null, null,
                    $"Medicine update failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the medicine item." });
            }
        }

        // Delete individual medicine item with audit logging
        [HttpPost]
        public async Task<IActionResult> DeleteMedicineItem(int indentItemId)
        {
            try
            {
                // Log medicine delete attempt
                await _auditService.LogAsync("compounder_indent", "DEL_MEDICINE", indentItemId.ToString(), null, null,
                    $"Medicine delete attempted - ItemId: {indentItemId}");

                // Get user roles
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();
                var existingItem = await _repo.GetItemByIdAsync(indentItemId);
                if (existingItem == null)
                {
                    await _auditService.LogAsync("compounder_indent", "DEL_MED_NOTFND", indentItemId.ToString(), null, null,
                        "Medicine delete failed - item not found");
                    return Json(new { success = false, message = "Medicine item not found." });
                }
                // Get the indent to check status and permissions
                var indent = await _repo.GetByIdAsync(existingItem.IndentId);
                if (indent == null)
                {
                    await _auditService.LogAsync("compounder_indent", "DEL_MED_INDENT", existingItem.IndentId.ToString(), existingItem, null,
                        "Medicine delete failed - indent not found");
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }
                // MODIFIED: Check if this is Compounder Inventory mode (both approved and pending)
                var isCompounderInventory = (indent?.Status == "Approved" || indent?.Status == "Pending");

                // Security check: Compounder users cannot delete medicines from Compounder Inventory
                if (isCompounderUser && isCompounderInventory)
                {
                    await _auditService.LogAsync("compounder_indent", "DEL_MED_DENY", indentItemId.ToString(), existingItem, null,
                        "Medicine delete denied - compounder user cannot delete from inventory");
                    return Json(new { success = false, message = "Access denied. Compounder users cannot delete medicines from Compounder Inventory." });
                }
                // Security check: Allow deleting from both pending and approved indents in Compounder Inventory, or by doctors during approval
                var currentUser = User.Identity?.Name;
                if (!isCompounderInventory && indent.Status != "Pending" && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("compounder_indent", "DEL_MED_STATUS", indentItemId.ToString(), existingItem, null,
                        $"Medicine delete denied - wrong status: {indent.Status}");
                    return Json(new { success = false, message = "Only pending indents can be modified, or doctors can modify during approval." });
                }
                // Additional check for draft indents - only creator can delete
                if (indent.IndentType == "Draft Indent" && indent.CreatedBy != currentUser && userRole?.ToLower() != "doctor")
                {
                    await _auditService.LogAsync("compounder_indent", "DEL_MED_OWNER", indentItemId.ToString(), existingItem, null,
                        $"Medicine delete denied - not owner: {currentUser} vs {indent.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only delete items from your own drafts." });
                }
                // Delete the item
                await _repo.DeleteItemAsync(indentItemId);
                // Log successful medicine deletion
                await _auditService.LogAsync("compounder_indent", "DEL_MED_OK", indentItemId.ToString(),
                    existingItem, null, "Medicine item deleted successfully");

                return Json(new { success = true, message = "Medicine item deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "DEL_MED_FAIL", indentItemId.ToString(), null, null,
                    $"Medicine delete failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the medicine item." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            CompounderIndent itemToDelete = null;

            try
            {
                // Get entity before deletion for audit
                itemToDelete = await _repo.GetByIdAsync(id);
                if (itemToDelete == null)
                {
                    await _auditService.LogAsync("compounder_indent", "DELETE_NOTFND", id.ToString(), null, null,
                        $"Delete attempted on non-existent indent: {id}");
                    return Json(new { success = false, message = "Compounder Indent not found." });
                }
                // Log delete attempt
                await _auditService.LogAsync("compounder_indent", "DELETE_ATTEMPT", id.ToString(), itemToDelete, null,
                    $"Indent deletion attempted - Type: {itemToDelete.IndentType}, Status: {itemToDelete.Status}");
                // Get user roles
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();

                // Security check: Doctors cannot delete indents
                if (isDoctor)
                {
                    await _auditService.LogAsync("compounder_indent", "DELETE_DOC_DEN", id.ToString(), itemToDelete, null,
                        "Delete denied - doctors cannot delete indents");
                    return Json(new { success = false, message = "Access denied. Doctors can only review indents, not delete them." });
                }

                // Security check: Store users cannot delete indents
                if (isStoreUser)
                {
                    await _auditService.LogAsync("compounder_indent", "DELETE_STR_DEN", id.ToString(), itemToDelete, null,
                        "Delete denied - store users cannot delete indents");
                    return Json(new { success = false, message = "Access denied. Store users cannot delete indents." });
                }

                // Security check: Only allow deleting drafts by their creators
                //var currentUser = User.Identity?.Name;
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();
                if (itemToDelete.IndentType == "Draft Indent" && itemToDelete.CreatedBy != currentUser)
                {
                    await _auditService.LogAsync("compounder_indent", "DELETE_OWNER", id.ToString(), itemToDelete, null,
                        $"Delete denied - not owner: {currentUser} vs {itemToDelete.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only delete your own drafts." });
                }

                // Additional check: Don't allow deleting approved/rejected indents
                if (itemToDelete.Status == "Approved" || itemToDelete.Status == "Rejected")
                {
                    await _auditService.LogAsync("compounder_indent", "DELETE_STATUS", id.ToString(), itemToDelete, null,
                        $"Delete denied - status: {itemToDelete.Status}");
                    return Json(new { success = false, message = "Cannot delete approved or rejected indents." });
                }
                var userPlantId = await GetCurrentUserPlantIdAsync();
                await _repo.DeleteAsync(id, userPlantId);
                // Log successful deletion
                await _auditService.LogAsync("compounder_indent", "DELETE_SUCCESS", id.ToString(),
                    itemToDelete, null, $"Indent deleted successfully - Type: {itemToDelete.IndentType}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log delete failure
                await _auditService.LogAsync("compounder_indent", "DELETE_FAILED", id.ToString(), itemToDelete, null,
                    $"Indent deletion failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the indent." });
            }
        }
        public async Task<IActionResult> Details(int id, string indentType = null)
        {
            try
            {
                // Log details view attempt
                await _auditService.LogAsync("compounder_indent", "DETAILS_VIEW", id.ToString(), null, null,
                    $"Details view attempted - IndentType: {indentType}");
                var item = await _repo.GetByIdWithItemsAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("compounder_indent", "DETAILS_NOTFND", id.ToString(), null, null,
                        "Details view failed - indent not found");
                    return NotFound();
                }
                // Populate TotalReceivedFromStore for each medicine item
                foreach (var medicine in item.CompounderIndentItems)
                {
                    medicine.TotalReceivedFromStore = await _storeRepo.GetTotalReceivedFromStoreAsync(medicine.MedItemId);
                }
                // Security check: Only allow viewing drafts by their creators
                var currentUser = User.Identity?.Name;
                if (item.IndentType == "Draft Indent" && item.CreatedBy != currentUser)
                {
                    await _auditService.LogAsync("compounder_indent", "DETAILS_OWNER", id.ToString(), item, null,
                        $"Details view denied - not owner: {currentUser} vs {item.CreatedBy}");
                    return Json(new { success = false, message = "Access denied. You can only view your own drafts." });
                }

                // Set ViewBag flag for Compounder Inventory mode
                ViewBag.IsCompounderInventoryMode = indentType == "Compounder Inventory";

                // Log successful details view
                await _auditService.LogAsync("compounder_indent", "DETAILS_OK", id.ToString(), null, null,
                    $"Details viewed successfully - Type: {item.IndentType}, Status: {item.Status}");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "DETAILS_ERROR", id.ToString(), null, null,
                    $"Details view error: {ex.Message}");
                throw;
            }
        }
        public async Task<IActionResult> GetMedicineDetails(int medItemId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                // Log medicine details access
                await _auditService.LogAsync("compounder_indent", "MED_DETAILS", medItemId.ToString(), null, null,
                    $"Medicine details requested: {medItemId}");

                var medicine = await _repo.GetMedicineByIdAsync(medItemId);
                if (medicine == null)
                {
                    await _auditService.LogAsync("compounder_indent", "MED_DET_NOTFND", medItemId.ToString(), null, null,
                        "Medicine details not found");
                    return Json(new { success = false, message = "Medicine not found" });
                }

                // UPDATED: Get available stock from store batches
                var totalAvailableStockFromStore = await _storeRepo.GetTotalAvailableStockFromStoreAsync(medItemId, userPlantId);

                // Log successful access
                await _auditService.LogAsync("compounder_indent", "MED_DET_OK", medItemId.ToString(), null, null,
                    $"Medicine details accessed: {medicine.MedItemName}, Store available stock: {totalAvailableStockFromStore}");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        medItemId = medicine.MedItemId,
                        medItemName = medicine.MedItemName,
                        companyName = medicine.CompanyName ?? "Not Defined",
                        reorderLimit = medicine.ReorderLimit,
                        totalReceivedFromStore = totalAvailableStockFromStore  // This now shows available stock
                    }
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "MED_DET_ERROR", medItemId.ToString(), null, null,
                    $"Medicine details error: {ex.Message}");
                return Json(new { success = false, message = "Error loading medicine details" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMedicineItems(int indentId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync(); // ADD THIS

                var items = await _repo.GetItemsByIndentIdAsync(indentId, userPlantId);
                var result = new List<object>();

                foreach (var item in items.Select((value, index) => new { value, index }))
                {
                    // MAKE SURE THIS LINE INCLUDES userPlantId:
                    var totalAvailableStockFromStore = await _repo.GetTotalAvailableStockFromStoreAsync(item.value.MedItemId, userPlantId);

                    result.Add(new
                    {
                        indentItemId = item.value.IndentItemId,
                        slNo = item.index + 1,
                        medItemId = item.value.MedItemId,
                        medItemName = item.value.MedMaster?.MedItemName,
                        companyName = item.value.MedMaster?.CompanyName ?? "Not Defined",
                        vendorCode = item.value.VendorCode,
                        raisedQuantity = item.value.RaisedQuantity,
                        receivedQuantity = item.value.ReceivedQuantity,
                        pendingQuantity = item.value.PendingQuantity,
                        unitPrice = item.value.UnitPrice,
                        totalAmount = item.value.TotalAmount,
                        batchNo = item.value.BatchNo,
                        expiryDate = item.value.ExpiryDate?.ToString("yyyy-MM-dd"),
                        availableStock = item.value.AvailableStock,
                        totalReceivedFromStore = totalAvailableStockFromStore
                    });
                }

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred while loading medicine items." });
            }
        }
        // UpdateMedicineItemWithReason with audit logging
        [HttpPost]
        public async Task<IActionResult> UpdateMedicineItemWithReason(int indentItemId, int? medItemId = null,
            string vendorCode = null, int? raisedQuantity = null, int? receivedQuantity = null,
            decimal? unitPrice = null, string batchNo = null, DateTime? expiryDate = null,
            int? availableStock = null, string reason = null)
        {
            try
            {
                // Log medicine update with reason attempt
                await _auditService.LogAsync("compounder_indent", "UPD_MED_REASON", indentItemId.ToString(), null, null,
                    $"Medicine update with reason attempted - ItemId: {indentItemId}");
                var userRole = await GetUserRoleAsync();
                var isDoctor = userRole?.ToLower() == "doctor";
                var isStoreUser = await IsStoreUserAsync();
                var isCompounderUser = await IsCompounderUserAsync();
                // Get existing item first to check context
                var existingItem = await _repo.GetItemByIdAsync(indentItemId);
                if (existingItem == null)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_REAS_NOTFD", indentItemId.ToString(), null, null,
                        "Medicine update with reason failed - item not found");
                    return Json(new { success = false, message = "Medicine item not found." });
                }
                // Get the indent to check if it's approved (Compounder Inventory mode)
                var indent = await _repo.GetByIdAsync(existingItem.IndentId);
                // MODIFIED: Check if this is Compounder Inventory mode (both approved and pending)
                var isCompounderInventory = (indent?.Status == "Approved" || indent?.Status == "Pending");

                // Security check: Compounder users cannot edit Compounder Inventory items
                if (isCompounderUser && isCompounderInventory)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_REAS_DENY", indentItemId.ToString(), existingItem, null,
                        "Medicine update with reason denied - compounder user cannot edit inventory");
                    return Json(new { success = false, message = "Access denied. Compounder users cannot edit Compounder Inventory items." });
                }
                if (!isCompounderInventory)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_REAS_MODE", indentItemId.ToString(), existingItem, null,
                        "Medicine update with reason denied - not inventory mode");
                    return Json(new { success = false, message = "Edit with reason is only available for Compounder Inventory items." });
                }
                // Sanitize inputs
                if (vendorCode != null)
                {
                    vendorCode = SanitizeString(vendorCode);
                    if (!IsVendorCodeSecure(vendorCode))
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_INSEC", indentItemId.ToString(), existingItem, null,
                            $"Medicine update with reason blocked - insecure vendor code: {vendorCode}");
                        return Json(new { success = false, message = "Invalid vendor code format. Please remove any unsafe characters." });
                    }
                }
                if (batchNo != null)
                {
                    batchNo = SanitizeString(batchNo);
                    if (!IsBatchNoSecure(batchNo))
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_BATCH", indentItemId.ToString(), existingItem, null,
                            $"Medicine update with reason blocked - insecure batch: {batchNo}");
                        return Json(new { success = false, message = "Invalid batch number format. Please remove any unsafe characters." });
                    }
                }
                reason = SanitizeString(reason);

                // Security validation for reason
                if (!IsCommentsSecure(reason))
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_REAS_COMM", indentItemId.ToString(), existingItem, null,
                        "Medicine update with reason blocked - insecure reason");
                    return Json(new { success = false, message = "Invalid characters in edit reason. Please remove any script tags or unsafe characters." });
                }

                // Validate reason
                if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_REAS_SHORT", indentItemId.ToString(), existingItem, null,
                        "Medicine update with reason failed - insufficient reason");
                    return Json(new { success = false, message = "Please provide a detailed reason (minimum 10 characters) for editing this fully received item." });
                }

                if (reason.Length > 500)
                {
                    await _auditService.LogAsync("compounder_indent", "UPD_REAS_LONG", indentItemId.ToString(), existingItem, null,
                        "Medicine update with reason failed - reason too long");
                    return Json(new { success = false, message = "Edit reason cannot exceed 500 characters." });
                }

                // ENHANCED: Comprehensive expiry date validation
                if (expiryDate.HasValue)
                {
                    var expiryValidation = ValidateExpiryDate(expiryDate);
                    if (!expiryValidation.IsValid)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_EXP", indentItemId.ToString(), existingItem, null,
                            $"Medicine update with reason blocked - invalid expiry: {expiryValidation.ErrorMessage}");
                        return Json(new { success = false, message = expiryValidation.ErrorMessage });
                    }
                }
                // Store original values for audit trail
                var originalItem = new
                {
                    RaisedQuantity = existingItem.RaisedQuantity,
                    ReceivedQuantity = existingItem.ReceivedQuantity,
                    UnitPrice = existingItem.UnitPrice,
                    BatchNo = existingItem.BatchNo,
                    ExpiryDate = existingItem.ExpiryDate,
                    AvailableStock = existingItem.AvailableStock
                };

                // Update fields if provided
                if (medItemId.HasValue)
                    existingItem.MedItemId = medItemId.Value;

                if (!string.IsNullOrEmpty(vendorCode))
                    existingItem.VendorCode = vendorCode;

                if (raisedQuantity.HasValue)
                {
                    if (raisedQuantity.Value <= 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_QTYINV", indentItemId.ToString(), existingItem, null,
                            "Medicine update with reason blocked - invalid raised quantity");
                        return Json(new { success = false, message = "Raised quantity must be greater than 0." });
                    }
                    existingItem.RaisedQuantity = raisedQuantity.Value;
                }

                if (receivedQuantity.HasValue)
                {
                    if (receivedQuantity.Value < 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_RCVNEG", indentItemId.ToString(), existingItem, null,
                            "Medicine update with reason blocked - negative received quantity");
                        return Json(new { success = false, message = "Received quantity cannot be negative." });
                    }

                    if (receivedQuantity.Value > existingItem.RaisedQuantity)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_EXCEED", indentItemId.ToString(), existingItem, null,
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
                // Update batch number, expiry date, and available stock (only in Compounder Inventory mode)
                if (batchNo != null)
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

                if (availableStock.HasValue)
                {
                    if (availableStock.Value < 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "UPD_REAS_STKNG", indentItemId.ToString(), existingItem, null,
                            "Medicine update with reason blocked - negative available stock");
                        return Json(new { success = false, message = "Available stock cannot be negative." });
                    }
                    existingItem.AvailableStock = availableStock.Value;
                }
                else if (Request.Form.ContainsKey("availableStock"))
                {
                    existingItem.AvailableStock = null;
                }
                await _repo.UpdateItemAsync(existingItem);

                // Log the edit with reason (critical operation)
                await _auditService.LogAsync("compounder_indent", "UPD_REAS_OK", indentItemId.ToString(),
                    originalItem, existingItem, $"Medicine item updated with reason: {reason}");
                var responseMessage = "Medicine item updated successfully with reason!";
                var warningMessage = "";

                if (existingItem.ExpiryDate.HasValue)
                {
                    var daysToExpiry = (existingItem.ExpiryDate.Value.Date - DateTime.Today).Days;
                    if (daysToExpiry <= 30 && daysToExpiry >= 0)
                    {
                        warningMessage = $"Warning: This medicine expires in {daysToExpiry} days.";
                    }
                    else if (daysToExpiry < 0)
                    {
                        warningMessage = $"Warning: This medicine expired {Math.Abs(daysToExpiry)} days ago.";
                    }
                }

                // Return updated values for UI refresh
                return Json(new
                {
                    success = true,
                    message = responseMessage,
                    warning = warningMessage,
                    data = new
                    {
                        receivedQuantity = existingItem.ReceivedQuantity,
                        pendingQuantity = existingItem.PendingQuantity,
                        unitPrice = existingItem.UnitPrice,
                        totalAmount = existingItem.TotalAmount,
                        batchNo = existingItem.BatchNo,
                        expiryDate = existingItem.ExpiryDate?.ToString("yyyy-MM-dd"),
                        availableStock = existingItem.AvailableStock,
                        daysToExpiry = existingItem.ExpiryDate.HasValue ?
                            (existingItem.ExpiryDate.Value.Date - DateTime.Today).Days : (int?)null
                    }
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "UPD_REAS_FAIL", indentItemId.ToString(), null, null,
                    $"Medicine update with reason failed: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating the medicine item." });
            }
        }



        // ENHANCED: Validates expiry date to ensure it's not in the past
        private (bool IsValid, string ErrorMessage) ValidateExpiryDate(DateTime? expiryDate)
        {
            if (!expiryDate.HasValue)
                return (true, string.Empty); // Allow null values

            if (expiryDate.Value.Date < DateTime.Today)
            {
                return (false, "Expiry date cannot be in the past. Please select today's date or a future date.");
            }

            // Optional: Add warning for items expiring within 30 days
            var thirtyDaysFromNow = DateTime.Today.AddDays(30);
            return (true, string.Empty);
        }

        // ENHANCED: ProcessMedicinesForCreate with audit logging and stock validation
        private async Task<MedicineProcessResult> ProcessMedicinesForCreateEnhanced(int indentId, string medicinesJson, int? userPlantId)
        {
            try
            {
                await _auditService.LogAsync("compounder_indent", "PROC_MED_CRT", indentId.ToString(), null, null,
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

                foreach (var med in medicines)
                {
                    med.VendorCode = SanitizeString(med.VendorCode);
                    med.BatchNo = SanitizeString(med.BatchNo);

                    if (!IsVendorCodeSecure(med.VendorCode))
                    {
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid vendor code format: {med.VendorCode}" };
                    }

                    if (!string.IsNullOrEmpty(med.BatchNo) && !IsBatchNoSecure(med.BatchNo))
                    {
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid batch number format: {med.BatchNo}" };
                    }

                    if (med.RaisedQuantity > 0)
                    {
                        var totalStoreStock = await _repo.GetTotalAvailableStockFromStoreAsync(med.MedItemId, userPlantId);
                        if (med.RaisedQuantity > totalStoreStock)
                        {
                            return new MedicineProcessResult
                            {
                                Success = false,
                                ErrorMessage = $"Raised quantity ({med.RaisedQuantity}) for {med.MedItemName} cannot exceed available stock in store ({totalStoreStock})."
                            };
                        }

                        if (totalStoreStock <= 0)
                        {
                            return new MedicineProcessResult
                            {
                                Success = false,
                                ErrorMessage = $"Cannot add {med.MedItemName} as there is no available stock in store."
                            };
                        }
                    }
                }

                var newMedicines = medicines.Where(m => m.IsNew).ToList();
                foreach (var medicine in newMedicines)
                {
                    var item = new CompounderIndentItem
                    {
                        IndentId = indentId,
                        MedItemId = medicine.MedItemId,
                        VendorCode = medicine.VendorCode,
                        RaisedQuantity = medicine.RaisedQuantity,
                        ReceivedQuantity = medicine.ReceivedQuantity,
                        UnitPrice = medicine.UnitPrice,
                        TotalAmount = medicine.TotalAmount,
                        BatchNo = medicine.BatchNo,
                        ExpiryDate = medicine.ExpiryDate,
                        AvailableStock = medicine.AvailableStock
                    };
                    await _repo.AddItemAsync(item);
                }

                await _auditService.LogAsync("compounder_indent", "PROC_MED_OK", indentId.ToString(), null, null,
                    $"Medicine processing successful - Added: {newMedicines.Count} medicines, Plant: {userPlantId}");
                return new MedicineProcessResult { Success = true, HasMedicines = true };
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "PROC_MED_FAIL", indentId.ToString(), null, null,
                    $"Medicine processing failed: {ex.Message}");
                return new MedicineProcessResult { Success = false, ErrorMessage = $"Error processing medicines data: {ex.Message}" };
            }
        }
        // ProcessMedicinesForEdit with audit logging and stock validation
        private async Task<MedicineProcessResult> ProcessMedicinesForEditEnhanced(int indentId, string medicinesJson)
        {
            try
            {
                // Log medicine processing attempt for edit
                await _auditService.LogAsync("compounder_indent", "PROC_MED_EDT", indentId.ToString(), null, null,
                    $"Medicine processing for edit - IndentId: {indentId}");

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

                // Sanitize medicine data and validate against stock
                foreach (var med in medicines)
                {
                    med.VendorCode = SanitizeString(med.VendorCode);
                    med.BatchNo = SanitizeString(med.BatchNo);

                    if (!IsVendorCodeSecure(med.VendorCode))
                    {
                        await _auditService.LogAsync("compounder_indent", "PROC_EDT_VSEC", indentId.ToString(), null, null,
                            $"Edit medicine processing blocked - insecure vendor code: {med.VendorCode}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid vendor code format: {med.VendorCode}" };
                    }

                    if (!string.IsNullOrEmpty(med.BatchNo) && !IsBatchNoSecure(med.BatchNo))
                    {
                        await _auditService.LogAsync("compounder_indent", "PROC_EDT_BSEC", indentId.ToString(), null, null,
                            $"Edit medicine processing blocked - insecure batch: {med.BatchNo}");
                        return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid batch number format: {med.BatchNo}" };
                    }

                    // Validate expiry dates
                    if (med.ExpiryDate.HasValue)
                    {
                        var expiryValidation = ValidateExpiryDate(med.ExpiryDate);
                        if (!expiryValidation.IsValid)
                        {
                            await _auditService.LogAsync("compounder_indent", "PROC_EDT_DEXP", indentId.ToString(), null, null,
                                $"Edit medicine processing blocked - invalid expiry for {med.MedItemName}");
                            return new MedicineProcessResult { Success = false, ErrorMessage = $"Invalid expiry date for {med.MedItemName}: {expiryValidation.ErrorMessage}" };
                        }
                    }

                    // NEW: Validate raised quantity against available stock in store for new medicines
                    if (med.IsNew && med.RaisedQuantity > 0)
                    {
                        var totalStoreStock = await _storeRepo.GetTotalAvailableStockFromStoreAsync(med.MedItemId);
                        if (med.RaisedQuantity > totalStoreStock)
                        {
                            await _auditService.LogAsync("compounder_indent", "PROC_EDT_STOCK", indentId.ToString(), null, null,
                                $"Edit medicine processing blocked - raised qty ({med.RaisedQuantity}) exceeds store stock ({totalStoreStock}) for {med.MedItemName}");
                            return new MedicineProcessResult
                            {
                                Success = false,
                                ErrorMessage = $"Raised quantity ({med.RaisedQuantity}) for {med.MedItemName} cannot exceed available stock in store ({totalStoreStock})."
                            };
                        }

                        if (totalStoreStock <= 0)
                        {
                            await _auditService.LogAsync("compounder_indent", "PROC_EDT_NO_STOCK", indentId.ToString(), null, null,
                                $"Edit medicine processing blocked - no stock available for {med.MedItemName}");
                            return new MedicineProcessResult
                            {
                                Success = false,
                                ErrorMessage = $"Cannot add {med.MedItemName} as there is no available stock in store."
                            };
                        }
                    }
                    // For existing medicines being updated, we validate during the update process
                    else if (!med.IsNew && med.RaisedQuantity > 0)
                    {
                        var totalStoreStock = await _storeRepo.GetTotalReceivedFromStoreAsync(med.MedItemId);
                        if (med.RaisedQuantity > totalStoreStock)
                        {
                            await _auditService.LogAsync("compounder_indent", "PROC_EDT_UPD_STOCK", indentId.ToString(), null, null,
                                $"Edit medicine processing blocked - updated raised qty ({med.RaisedQuantity}) exceeds store stock ({totalStoreStock}) for {med.MedItemName}");
                            return new MedicineProcessResult
                            {
                                Success = false,
                                ErrorMessage = $"Updated raised quantity ({med.RaisedQuantity}) for {med.MedItemName} cannot exceed available stock in store ({totalStoreStock})."
                            };
                        }
                    }
                }

                // Get existing medicines
                var existingItems = await _repo.GetItemsByIndentIdAsync(indentId);
                var existingIds = existingItems.Select(x => x.IndentItemId).ToList();
                var submittedExistingIds = medicines.Where(m => !m.IsNew && m.IndentItemId.HasValue)
                                                  .Select(m => m.IndentItemId.Value).ToList();

                // Delete removed medicines
                var toDelete = existingIds.Except(submittedExistingIds).ToList();
                foreach (var deleteId in toDelete)
                {
                    await _repo.DeleteItemAsync(deleteId);
                    // Log medicine deletion during edit
                    await _auditService.LogAsync("compounder_indent", "PROC_EDT_DEL", deleteId.ToString(), null, null,
                        $"Medicine deleted during edit - ItemId: {deleteId}");
                }

                // Validate remaining medicines
                var validationResult = await ValidateMedicinesEnhanced(indentId, medicines, submittedExistingIds);
                if (!validationResult.Success)
                {
                    await _auditService.LogAsync("compounder_indent", "PROC_EDT_VFAIL", indentId.ToString(), null, null,
                        $"Edit medicine validation failed: {validationResult.ErrorMessage}");
                    return validationResult;
                }

                // Update existing medicines
                foreach (var medicine in medicines.Where(m => !m.IsNew && m.IndentItemId.HasValue))
                {
                    var existingItem = existingItems.FirstOrDefault(x => x.IndentItemId == medicine.IndentItemId.Value);
                    if (existingItem != null)
                    {
                        existingItem.VendorCode = medicine.VendorCode;
                        existingItem.RaisedQuantity = medicine.RaisedQuantity;
                        existingItem.ReceivedQuantity = medicine.ReceivedQuantity;
                        existingItem.UnitPrice = medicine.UnitPrice;
                        existingItem.TotalAmount = medicine.TotalAmount;
                        existingItem.BatchNo = medicine.BatchNo;
                        existingItem.ExpiryDate = medicine.ExpiryDate;
                        existingItem.AvailableStock = medicine.AvailableStock;
                        await _repo.UpdateItemAsync(existingItem);
                    }
                }

                // Add new medicines
                var newMedicinesCount = 0;
                foreach (var medicine in medicines.Where(m => m.IsNew))
                {
                    var item = new CompounderIndentItem
                    {
                        IndentId = indentId,
                        MedItemId = medicine.MedItemId,
                        VendorCode = medicine.VendorCode,
                        RaisedQuantity = medicine.RaisedQuantity,
                        ReceivedQuantity = medicine.ReceivedQuantity,
                        UnitPrice = medicine.UnitPrice,
                        TotalAmount = medicine.TotalAmount,
                        BatchNo = medicine.BatchNo,
                        ExpiryDate = medicine.ExpiryDate,
                        AvailableStock = medicine.AvailableStock
                    };

                    await _repo.AddItemAsync(item);
                    newMedicinesCount++;
                }

                // Log successful edit processing
                await _auditService.LogAsync("compounder_indent", "PROC_EDT_OK", indentId.ToString(), null, null,
                    $"Edit medicine processing successful - Added: {newMedicinesCount}, Deleted: {toDelete.Count}");

                return new MedicineProcessResult { Success = true, HasMedicines = true };
            }
            catch (Exception ex)
            {
                // Log edit medicine processing failure
                await _auditService.LogAsync("compounder_indent", "PROC_EDT_FAIL", indentId.ToString(), null, null,
                    $"Edit medicine processing failed: {ex.Message}");
                return new MedicineProcessResult { Success = false, ErrorMessage = $"Error processing medicines data: {ex.Message}" };
            }
        }

        // ENHANCED: ValidateMedicines with audit logging and stock validation
        private async Task<MedicineProcessResult> ValidateMedicinesEnhanced(int indentId,
            List<MedicineDto> medicines, List<int> excludeItemIds = null)
        {
            try
            {
                // Log validation attempt
                await _auditService.LogAsync("compounder_indent", "VALID_MED", indentId.ToString(), null, null,
                    $"Medicine validation started - Count: {medicines.Count}");

                var medicineIds = new HashSet<int>();
                var stockValidationErrors = new List<string>();

                foreach (var medicine in medicines)
                {
                    // Check for duplicate medicines within the submission
                    if (medicineIds.Contains(medicine.MedItemId))
                    {
                        await _auditService.LogAsync("compounder_indent", "VALID_MED_DUP", indentId.ToString(), null, null,
                            $"Medicine validation failed - duplicate in submission: {medicine.MedItemId}");
                        return new MedicineProcessResult
                        {
                            Success = false,
                            ErrorMessage = $"Duplicate medicine found in medicines list: {medicine.MedItemName}"
                        };
                    }
                    medicineIds.Add(medicine.MedItemId);

                    // NEW: Validate raised quantity against available stock
                    if (medicine.RaisedQuantity > 0)
                    {
                        var totalStoreStock = await _storeRepo.GetTotalAvailableStockFromStoreAsync(medicine.MedItemId);

                        if (medicine.RaisedQuantity > totalStoreStock)
                        {
                            var errorMsg = $"{medicine.MedItemName}: Raised quantity ({medicine.RaisedQuantity}) exceeds available stock ({totalStoreStock})";
                            stockValidationErrors.Add(errorMsg);

                            await _auditService.LogAsync("compounder_indent", "VALID_MED_STOCK", indentId.ToString(), null, null,
                                $"Medicine validation failed - stock insufficient: {errorMsg}");
                        }

                        if (totalStoreStock <= 0)
                        {
                            var errorMsg = $"{medicine.MedItemName}: No stock available in store";
                            stockValidationErrors.Add(errorMsg);

                            await _auditService.LogAsync("compounder_indent", "VALID_MED_NO_STOCK", indentId.ToString(), null, null,
                                $"Medicine validation failed - no stock: {medicine.MedItemName}");
                        }
                    }

                    // ENHANCED: Validate expiry date if provided
                    if (medicine.ExpiryDate.HasValue)
                    {
                        var expiryValidation = ValidateExpiryDate(medicine.ExpiryDate);
                        if (!expiryValidation.IsValid)
                        {
                            await _auditService.LogAsync("compounder_indent", "VALID_MED_EXP", indentId.ToString(), null, null,
                                $"Medicine validation failed - invalid expiry for {medicine.MedItemName}");
                            return new MedicineProcessResult
                            {
                                Success = false,
                                ErrorMessage = $"Invalid expiry date for {medicine.MedItemName}: {expiryValidation.ErrorMessage}"
                            };
                        }
                    }

                    // Existing validations for medicine uniqueness
                    var excludeId = medicine.IsNew ? null : medicine.IndentItemId;
                    if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medicine.MedItemId, excludeId))
                    {
                        await _auditService.LogAsync("compounder_indent", "VALID_MED_EXIST", indentId.ToString(), null, null,
                            $"Medicine validation failed - already exists: {medicine.MedItemId}");
                        return new MedicineProcessResult
                        {
                            Success = false,
                            ErrorMessage = $"Medicine {medicine.MedItemName} is already added to this indent."
                        };
                    }
                }

                // Check if there were any stock validation errors
                if (stockValidationErrors.Any())
                {
                    var combinedErrors = "Stock validation errors:\n" + string.Join("\n", stockValidationErrors.Take(5));
                    if (stockValidationErrors.Count > 5)
                    {
                        combinedErrors += $"\n... and {stockValidationErrors.Count - 5} more errors.";
                    }

                    await _auditService.LogAsync("compounder_indent", "VALID_MED_STOCKS", indentId.ToString(), null, null,
                        $"Medicine validation failed - multiple stock errors: {stockValidationErrors.Count}");

                    return new MedicineProcessResult
                    {
                        Success = false,
                        ErrorMessage = combinedErrors
                    };
                }

                // Log successful validation
                await _auditService.LogAsync("compounder_indent", "VALID_MED_OK", indentId.ToString(), null, null,
                    $"Medicine validation successful - Count: {medicines.Count}");

                return new MedicineProcessResult { Success = true };
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "VALID_MED_ERR", indentId.ToString(), null, null,
                    $"Medicine validation error: {ex.Message}");
                throw;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBatchesForMedicine(int indentItemId)
        {
            try
            {
                // Log batch access
                await _auditService.LogAsync("compounder_indent", "GET_BATCHES", indentItemId.ToString(), null, null,
                    $"Batches requested for medicine item: {indentItemId}");

                var batches = await _repo.GetBatchesByIndentItemIdAsync(indentItemId);

                // Log successful access
                await _auditService.LogAsync("compounder_indent", "GET_BATCH_OK", indentItemId.ToString(), null, null,
                    $"Batches loaded - Count: {batches.Count()}");

                return Json(new { success = true, data = batches });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "GET_BATCH_ERR", indentItemId.ToString(), null, null,
                    $"Get batches error: {ex.Message}");
                return Json(new { success = false, data = new List<object>() });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveBatchesForMedicine([FromBody] List<CompounderIndentBatchDto> batches)
        {
            try
            {
                if (batches == null || batches.Count == 0)
                {
                    await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_NON", "null", null, null,
                        "Save batches failed - no batch data received");
                    return Json(new { success = false, message = "No batch data received." });
                }

                var indentItemId = batches[0].IndentItemId;

                // Log batch save attempt
                await _auditService.LogAsync("compounder_indent", "SAVE_BATCHES", indentItemId.ToString(), null, null,
                    $"Save batches attempted for medicine item: {indentItemId}, Count: {batches.Count}");

                // Get the indent item to check the requested quantity
                var indentItem = await _repo.GetItemByIdAsync(indentItemId);
                if (indentItem == null)
                {
                    await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_NF", indentItemId.ToString(), null, null,
                        "Save batches failed - indent item not found");
                    return Json(new { success = false, message = "Indent item not found." });
                }

                var requestedQuantity = indentItem.RaisedQuantity;

                // Get existing batches to calculate differences
                var existingBatches = await _repo.GetBatchesByIndentItemIdAsync(indentItemId);

                // Group existing batches by batch number and sum their received quantities
                // This handles cases where the same batch was used multiple times previously
                var existingBatchesByBatchNo = existingBatches
                    .GroupBy(b => b.BatchNo)
                    .ToDictionary(g => g.Key, g => g.Sum(b => b.ReceivedQuantity));

                // Group new batches by batch number and sum quantities
                // This handles cases where the same batch is being used multiple times in current submission
                var newBatchesByBatchNo = batches
                    .GroupBy(b => b.BatchNo)
                    .ToDictionary(g => g.Key, g => g.Sum(b => b.ReceivedQuantity));

                // Allow duplicate batch numbers in submission since same batch can be used multiple times
                // But validate each entry individually
                var batchNumbers = batches.Select(b => b.BatchNo).ToList();
                Console.WriteLine($"Processing batch numbers: {string.Join(", ", batchNumbers)}");

                // Validate each batch and calculate store stock changes
                var storeStockUpdates = new List<StoreStockUpdate>();

                for (int i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    var batchNumber = i + 1;

                    // Basic validation
                    if (string.IsNullOrWhiteSpace(batch.BatchNo))
                    {
                        await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_VAL", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - empty batch number for batch {batchNumber}");
                        return Json(new { success = false, message = $"Batch number is required for batch {batchNumber}." });
                    }

                    if (batch.ExpiryDate.Date < DateTime.Today)
                    {
                        await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_EXP", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - past expiry date for batch {batch.BatchNo}");
                        return Json(new { success = false, message = $"Batch \"{batch.BatchNo}\" has an expiry date in the past. Please select today's date or a future date." });
                    }

                    // Validate received quantity
                    if (batch.ReceivedQuantity <= 0)
                    {
                        await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_QTY", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - invalid quantity for batch {batchNumber}");
                        return Json(new { success = false, message = $"Issued Qty must be greater than 0 for batch {batchNumber}." });
                    }

                    // Check if received quantity exceeds requested quantity
                    if (batch.ReceivedQuantity > requestedQuantity)
                    {
                        await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_EXC", indentItemId.ToString(), null, null,
                            $"Save batches validation failed - received exceeds requested for batch {batchNumber}");
                        return Json(new { success = false, message = $"Issued Qty ({batch.ReceivedQuantity}) cannot exceed requested quantity ({requestedQuantity}) for batch {batchNumber}." });
                    }
                }

                // Additional validation: Check if total received quantity exceeds requested quantity
                var totalReceivedQuantity = batches.Sum(b => b.ReceivedQuantity);
                if (totalReceivedQuantity > requestedQuantity)
                {
                    await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_TOT", indentItemId.ToString(), null, null,
                        $"Save batches validation failed - total received exceeds requested");
                    return Json(new { success = false, message = $"Total received quantity ({totalReceivedQuantity}) cannot exceed requested quantity ({requestedQuantity})." });
                }

                // Calculate store stock changes for each unique batch number
                foreach (var batchGroup in newBatchesByBatchNo)
                {
                    var batchNo = batchGroup.Key;
                    var newTotalIssued = batchGroup.Value;
                    var previousTotalIssued = existingBatchesByBatchNo.ContainsKey(batchNo) ? existingBatchesByBatchNo[batchNo] : 0;
                    var netIssuedDifference = newTotalIssued - previousTotalIssued;

                    Console.WriteLine($"Batch {batchNo}: Previous={previousTotalIssued}, New={newTotalIssued}, Difference={netIssuedDifference}");

                    if (netIssuedDifference != 0)
                    {
                        // Check if this batch exists in store
                        var userPlantId = await GetCurrentUserPlantIdAsync();
                        var storeBatch = await _repo.GetStoreBatchByBatchNoAndMedicineAsync(batchNo, indentItem.MedItemId, userPlantId);

                        if (storeBatch != null)
                        {
                            // This is a store batch, calculate stock update
                            var newStoreStock = storeBatch.AvailableStock - netIssuedDifference;

                            Console.WriteLine($"Store batch {batchNo}: Current stock={storeBatch.AvailableStock}, After update={newStoreStock}");

                            if (newStoreStock < 0)
                            {
                                await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_INSUFFICIENT", indentItemId.ToString(), null, null,
                                    $"Insufficient store stock for batch {batchNo}");
                                return Json(new
                                {
                                    success = false,
                                    message = $"Insufficient stock in store for batch \"{batchNo}\". Available: {storeBatch.AvailableStock}, Required additional: {netIssuedDifference}, Previous total issued: {previousTotalIssued}, New total requested: {newTotalIssued}"
                                });
                            }

                            storeStockUpdates.Add(new StoreStockUpdate
                            {
                                BatchId = storeBatch.BatchId,
                                BatchNo = batchNo,
                                PreviousStock = storeBatch.AvailableStock,
                                NewStock = newStoreStock,
                                IssuedQuantity = netIssuedDifference,
                                PreviousIssued = previousTotalIssued,
                                NewTotalIssued = newTotalIssued
                            });
                        }
                        else
                        {
                            Console.WriteLine($"Batch {batchNo} not found in store - treating as manual batch");
                        }
                    }
                }

                // All validations passed, proceed with updates
                // Create individual batch entities - preserve original structure to allow same batch multiple times
                var batchEntities = new List<CompounderIndentBatch>();

                foreach (var batch in batches)
                {
                    batchEntities.Add(new CompounderIndentBatch
                    {
                        BatchId = 0, // Always 0 for new entities - EF will auto-generate
                        IndentItemId = batch.IndentItemId,
                        BatchNo = batch.BatchNo,
                        ExpiryDate = batch.ExpiryDate,
                        ReceivedQuantity = batch.ReceivedQuantity,
                        VendorCode = batch.VendorCode ?? string.Empty,
                        AvailableStock = batch.AvailableStock
                    });
                }

                Console.WriteLine($"Creating {batchEntities.Count} batch entities for indent item {indentItemId}");

                // Save compounder batches (this replaces all existing batches)
                await _repo.AddOrUpdateBatchesAsync(indentItemId, batchEntities);

                // Update store stock for affected batches
                foreach (var stockUpdate in storeStockUpdates)
                {
                    await _repo.UpdateBatchAvailableStockAsync(stockUpdate.BatchId, stockUpdate.NewStock);

                    // Log store stock update
                    await _auditService.LogAsync("compounder_indent", "STORE_STOCK_UPDATE", stockUpdate.BatchId.ToString(),
                        new
                        {
                            PreviousStock = stockUpdate.PreviousStock,
                            PreviousIssued = stockUpdate.PreviousIssued,
                            NetIssuedDifference = stockUpdate.IssuedQuantity,
                            NewTotalIssued = stockUpdate.NewTotalIssued
                        },
                        new { NewStock = stockUpdate.NewStock },
                        $"Store stock updated for batch {stockUpdate.BatchNo}: {stockUpdate.PreviousStock} -> {stockUpdate.NewStock} (Net change: {stockUpdate.IssuedQuantity})");
                }

                // Log successful batch save
                await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_OK", indentItemId.ToString(), null, null,
                    $"Batches saved successfully - Count: {batchEntities.Count}, Total Received: {totalReceivedQuantity}, Store Updates: {storeStockUpdates.Count}");

                return Json(new
                {
                    success = true,
                    message = $"Batches saved successfully! {(storeStockUpdates.Count > 0 ? $"Updated {storeStockUpdates.Count} store batch(es)." : "")}",
                    totalReceived = batchEntities.Sum(x => x.ReceivedQuantity),
                    totalAvailable = batchEntities.Sum(x => x.AvailableStock),
                    totalBatches = batchEntities.Count,
                    requestedQuantity = requestedQuantity,
                    pendingQuantity = requestedQuantity - batchEntities.Sum(x => x.ReceivedQuantity),
                    batchDetails = batchEntities.Select(b => new {
                        batchNo = b.BatchNo,
                        receivedQuantity = b.ReceivedQuantity,
                        availableStock = b.AvailableStock,
                        expiryDate = b.ExpiryDate.ToString("yyyy-MM-dd")
                    }).ToList(),
                    storeUpdates = storeStockUpdates.Select(su => new {
                        batchNo = su.BatchNo,
                        previousStock = su.PreviousStock,
                        newStock = su.NewStock,
                        netChange = su.IssuedQuantity,
                        previousIssued = su.PreviousIssued,
                        newTotalIssued = su.NewTotalIssued
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "SAVE_BATCH_ERR", "unknown", null, null,
                    $"Save batches error: {ex.Message}");
                return Json(new { success = false, message = $"An error occurred while saving batch information: {ex.Message}" });
            }
        }


        [HttpGet]
        public async Task<IActionResult> ValidateBatchSelection(int medItemId, string selectedBatchNo)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                await _auditService.LogAsync("compounder_indent", "VALIDATE_BATCH", selectedBatchNo, null, null,
                    $"Batch selection validation requested - Medicine: {medItemId}, Batch: {selectedBatchNo}, Plant: {userPlantId}");

                // Get all available batches for this medicine
                var availableBatches = await _repo.GetAvailableStoreBatchesForMedicineAsync(medItemId, userPlantId);

                if (!availableBatches.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No batches available for this medicine.",
                        requiresPrompt = false
                    });
                }

                var selectedBatch = availableBatches.FirstOrDefault(b => b.BatchNo == selectedBatchNo);
                if (selectedBatch == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Selected batch not found.",
                        requiresPrompt = false
                    });
                }

                // Check if there's an earlier expiring batch available
                var batchesWithStock = availableBatches.Where(b => b.AvailableStock > 0).ToList();
                if (batchesWithStock.Count <= 1)
                {
                    // Only one batch available, no prompt needed
                    return Json(new
                    {
                        success = true,
                        requiresPrompt = false,
                        message = "Selection validated - only batch available."
                    });
                }

                var earliestBatch = batchesWithStock.OrderBy(b => b.ExpiryDate).First();

                // Check if user selected the earliest expiring batch
                if (selectedBatch.BatchNo == earliestBatch.BatchNo)
                {
                    return Json(new
                    {
                        success = true,
                        requiresPrompt = false,
                        message = "Earliest expiry batch selected - FIFO compliant."
                    });
                }

                // User selected a later-expiring batch - prompt required
                var comparisonData = new
                {
                    earliestBatch = new
                    {
                        batchNo = earliestBatch.BatchNo,
                        expiryDate = earliestBatch.ExpiryDate.ToString("yyyy-MM-dd"),
                        availableStock = earliestBatch.AvailableStock,
                        daysToExpiry = earliestBatch.DaysToExpiry,
                        expiryIndicator = earliestBatch.ExpiryIndicator
                    },
                    selectedBatch = new
                    {
                        batchNo = selectedBatch.BatchNo,
                        expiryDate = selectedBatch.ExpiryDate.ToString("yyyy-MM-dd"),
                        availableStock = selectedBatch.AvailableStock,
                        daysToExpiry = selectedBatch.DaysToExpiry,
                        expiryIndicator = selectedBatch.ExpiryIndicator
                    }
                };

                await _auditService.LogAsync("compounder_indent", "BATCH_PROMPT_REQ", selectedBatchNo, null, null,
                    $"Early expiry prompt required - Selected: {selectedBatch.BatchNo} ({selectedBatch.ExpiryDate:yyyy-MM-dd}), " +
                    $"Earliest: {earliestBatch.BatchNo} ({earliestBatch.ExpiryDate:yyyy-MM-dd})");

                return Json(new
                {
                    success = true,
                    requiresPrompt = true,
                    message = "Please select early expiry medicines per order in list.",
                    comparisonData = comparisonData
                });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "VALIDATE_BATCH_ERR", selectedBatchNo, null, null,
                    $"Batch validation error: {ex.Message}");

                return Json(new
                {
                    success = false,
                    message = "Error validating batch selection.",
                    requiresPrompt = false
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LogBatchSelectionDecision(string selectedBatchNo, string earliestBatchNo,
            bool usedEarliestBatch, string userDecision)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = User.Identity?.Name + " - " + User.GetFullName();

                var decisionDetails = new
                {
                    SelectedBatch = selectedBatchNo,
                    EarliestBatch = earliestBatchNo,
                    UsedEarliestBatch = usedEarliestBatch,
                    UserDecision = userDecision,
                    User = currentUser,
                    Plant = userPlantId,
                    Timestamp = DateTime.Now
                };

                await _auditService.LogAsync("compounder_indent", "BATCH_DECISION", selectedBatchNo, null, decisionDetails,
                    $"Batch selection decision - User {(usedEarliestBatch ? "selected earliest" : "overrode FIFO")} batch: " +
                    $"{(usedEarliestBatch ? earliestBatchNo : selectedBatchNo)}, Decision: {userDecision}");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "BATCH_DECISION_ERR", selectedBatchNo, null, null,
                    $"Error logging batch decision: {ex.Message}");

                return Json(new { success = false });
            }
        }
        // Helper class for store stock updates
        private class StoreStockUpdate
        {
            public int BatchId { get; set; }
            public string BatchNo { get; set; } = string.Empty;
            public int PreviousStock { get; set; }
            public int NewStock { get; set; }
            public int IssuedQuantity { get; set; }
            public int PreviousIssued { get; set; }
            public int NewTotalIssued { get; set; }
        }
        [HttpPost]
        public async Task<IActionResult> DeleteBatch(int indentItemId, int batchId = 0)
        {
            try
            {
                if (batchId > 0)
                {
                    // Log batch delete attempt
                    await _auditService.LogAsync("compounder_indent", "DELETE_BATCH", batchId.ToString(), null, null,
                        $"Batch deletion attempted: {batchId}");
                    var userPlant = await GetCurrentUserPlantIdAsync();
                    await _repo.DeleteBatchAsync(indentItemId,batchId, userPlant);

                    // Log successful batch deletion
                    await _auditService.LogAsync("compounder_indent", "DELETE_BATCH_OK", batchId.ToString(),
                        new { BatchId = batchId }, null, "Batch deleted successfully");

                    return Json(new { success = true,
                        message = "successfully removed the batch."
                    });
                }
                else
                {
                    return Json(new { success = true,
                        message = "No Batch found to delete."
                    });
                }
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "DELETE_BATCH_E", batchId.ToString(), null, null,
                        $"Batch deletion error: {ex.Message}");

                return Json(new { success = false, message = ex.Message });
            }
        }

        // DTO
        public class CompounderIndentBatchDto
        {
            public int? BatchId { get; set; }
            public int IndentItemId { get; set; }
            public string BatchNo { get; set; }
            public DateTime ExpiryDate { get; set; }
            public int ReceivedQuantity { get; set; }
            public string VendorCode { get; set; }
            public int AvailableStock { get; set; } = 0;
        }

        #region Private Methods for Input Sanitization and Validation

        private CompounderIndent SanitizeInput(CompounderIndent model)
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

        private bool IsInputSecure(CompounderIndent model)
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
        private async Task PopulateMedicineDropdownAsync()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();

            var allMedicines = await _repo.GetMedicinesAsync(userPlantId);

            // Filter medicines to only those with available stock in store > 0
            var medicinesWithStock = new List<MedMaster>();

            foreach (var medicine in allMedicines)
            {
                var availableStock = await _repo.GetTotalAvailableStockFromStoreAsync(medicine.MedItemId, userPlantId);
                if (availableStock > 0)
                {
                    medicinesWithStock.Add(medicine);
                }
            }
            var orderedMedicines = medicinesWithStock.OrderBy(m => m.MedItemName).ToList();
            ViewBag.MedicineList = new SelectList(orderedMedicines, "MedItemId", "MedItemName");
            //ViewBag.MedicineList = new SelectList(medicinesWithStock, "MedItemId", "MedItemName");
        }
        private async Task PopulateIndentTypeDropdownAsync(string createdBy = null)
        {
            var currentUser = User.Identity?.Name + " - " + User.GetFullName();
            var indentTypes = new List<SelectListItem>();

            indentTypes.Add(new SelectListItem { Value = "Pending Indents", Text = "Pending Indents" });
            indentTypes.Add(new SelectListItem { Value = "Approved Indents", Text = "Approved Indents" });
            indentTypes.Add(new SelectListItem { Value = "Rejected Indents", Text = "Rejected Indents" });

            if (string.IsNullOrEmpty(createdBy) || createdBy == currentUser)
            {
                indentTypes.Add(new SelectListItem { Value = "Draft Indent", Text = "Draft Indent" });
            }

            ViewBag.IndentTypeList = indentTypes;
        }

        private void SetStatusBasedOnIndentType(CompounderIndent model)
        {
            model.Status = model.IndentType switch
            {
                "Pending Indents" => "Pending",
                "Approved Indents" => "Approved",
                "Rejected Indents" => "Rejected",
                "Draft Indent" => "Draft",
                _ => "Pending"
            };
        }



        

        // Validate medicines with plant filtering
        private async Task<MedicineProcessResult> ValidateMedicinesEnhanced(int indentId,
            List<MedicineDto> medicines, int? userPlantId, List<int> excludeItemIds = null)
        {
            try
            {
                // Log validation attempt
                await _auditService.LogAsync("compounder_indent", "VALID_MED", indentId.ToString(), null, null,
                    $"Medicine validation started - Count: {medicines.Count}, Plant: {userPlantId}");

                var medicineIds = new HashSet<int>();
                var stockValidationErrors = new List<string>();

                foreach (var medicine in medicines)
                {
                    // Check for duplicate medicines within the submission
                    if (medicineIds.Contains(medicine.MedItemId))
                    {
                        await _auditService.LogAsync("compounder_indent", "VALID_MED_DUP", indentId.ToString(), null, null,
                            $"Medicine validation failed - duplicate in submission: {medicine.MedItemId}");
                        return new MedicineProcessResult
                        {
                            Success = false,
                            ErrorMessage = $"Duplicate medicine found in medicines list: {medicine.MedItemName}"
                        };
                    }
                    medicineIds.Add(medicine.MedItemId);

                    // Validate raised quantity against available stock with plant filtering
                    if (medicine.RaisedQuantity > 0)
                    {
                        var totalStoreStock = await _repo.GetTotalAvailableStockFromStoreAsync(medicine.MedItemId, userPlantId);

                        if (medicine.RaisedQuantity > totalStoreStock)
                        {
                            var errorMsg = $"{medicine.MedItemName}: Raised quantity ({medicine.RaisedQuantity}) exceeds available stock ({totalStoreStock})";
                            stockValidationErrors.Add(errorMsg);

                            await _auditService.LogAsync("compounder_indent", "VALID_MED_STOCK", indentId.ToString(), null, null,
                                $"Medicine validation failed - stock insufficient: {errorMsg}");
                        }

                        if (totalStoreStock <= 0)
                        {
                            var errorMsg = $"{medicine.MedItemName}: No stock available in store";
                            stockValidationErrors.Add(errorMsg);

                            await _auditService.LogAsync("compounder_indent", "VALID_MED_NO_STOCK", indentId.ToString(), null, null,
                                $"Medicine validation failed - no stock: {medicine.MedItemName}");
                        }
                    }

                    // Existing validations for medicine uniqueness with plant filtering
                    var excludeId = medicine.IsNew ? null : medicine.IndentItemId;
                    if (await _repo.IsMedicineAlreadyAddedAsync(indentId, medicine.MedItemId, excludeId, userPlantId))
                    {
                        await _auditService.LogAsync("compounder_indent", "VALID_MED_EXIST", indentId.ToString(), null, null,
                            $"Medicine validation failed - already exists: {medicine.MedItemId}");
                        return new MedicineProcessResult
                        {
                            Success = false,
                            ErrorMessage = $"Medicine {medicine.MedItemName} is already added to this indent."
                        };
                    }
                }

                // Check if there were any stock validation errors
                if (stockValidationErrors.Any())
                {
                    var combinedErrors = "Stock validation errors:\n" + string.Join("\n", stockValidationErrors.Take(5));
                    if (stockValidationErrors.Count > 5)
                    {
                        combinedErrors += $"\n... and {stockValidationErrors.Count - 5} more errors.";
                    }

                    await _auditService.LogAsync("compounder_indent", "VALID_MED_STOCKS", indentId.ToString(), null, null,
                        $"Medicine validation failed - multiple stock errors: {stockValidationErrors.Count}");

                    return new MedicineProcessResult
                    {
                        Success = false,
                        ErrorMessage = combinedErrors
                    };
                }

                // Log successful validation
                await _auditService.LogAsync("compounder_indent", "VALID_MED_OK", indentId.ToString(), null, null,
                    $"Medicine validation successful - Count: {medicines.Count}, Plant: {userPlantId}");

                return new MedicineProcessResult { Success = true };
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("compounder_indent", "VALID_MED_ERR", indentId.ToString(), null, null,
                    $"Medicine validation error: {ex.Message}");
                throw;
            }
        }

        private bool IsVendorCodeSecure(string vendorCode)
        {
            if (string.IsNullOrEmpty(vendorCode)) return true;

            // Vendor code should only contain alphanumeric characters, hyphens, and underscores
            return Regex.IsMatch(vendorCode, @"^[a-zA-Z0-9\-_]+$");
        }

        private bool IsBatchNoSecure(string batchNo)
        {
            if (string.IsNullOrEmpty(batchNo)) return true;

            // Batch number should only contain alphanumeric characters, hyphens, underscores, and dots
            return Regex.IsMatch(batchNo, @"^[a-zA-Z0-9\-_\.]+$");
        }

        #endregion

        // Helper classes for medicine processing
        public class MedicineProcessResult
        {
            public bool Success { get; set; }
            public bool HasMedicines { get; set; }
            public string ErrorMessage { get; set; }
        }
        public class MedicineDto
        {
            public int? IndentItemId { get; set; }
            public string TempId { get; set; }
            public int MedItemId { get; set; }
            public string MedItemName { get; set; }
            public string? CompanyName { get; set; }
            public string? VendorCode { get; set; }
            public int RaisedQuantity { get; set; }
            public int ReceivedQuantity { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? TotalAmount { get; set; }
            public string? BatchNo { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public int? AvailableStock { get; set; }
            public bool IsNew { get; set; }
        }
    }
}