using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessSysAttachScreenRole")]
    public class SysAttachScreenRoleController : Controller
    {
        private readonly ISysAttachScreenRoleRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public SysAttachScreenRoleController(ISysAttachScreenRoleRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        // GET: /SysAttachScreenRole
        public IActionResult Index() => View();

        // AJAX for DataTable
        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListWithBaseAsync();
                var screenList = await _repo.GetScreenListAsync();

                var result = list.Select(x => new
                {
                    x.uid,
                    role_name = x.SysRole?.role_name ?? "",
                    screen_names = string.Join(", ",
                        x.screen_uids.Select(id =>
                            screenList.FirstOrDefault(s => s.screen_uid == id)?.screen_name ?? $"[ID:{id}]"
                        )
                    ),
                    x.CreatedBy,
                    x.CreatedOn,
                    x.ModifiedBy,
                    x.ModifiedOn
                });

                // Log data access for security monitoring
                await _auditService.LogAsync("sys_attach_screen_role", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} screen-role assignments for listing");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("sys_attach_screen_role", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load screen-role assignments: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        // GET: create form partial
        public async Task<IActionResult> Create()
        {
            try
            {
                // Log form access
                _ = Task.Run(async () => await _auditService.LogAsync("sys_attach_screen_role", "CREATE_FORM_VIEW", "new", null, null,
                    "Create screen-role assignment form accessed"));

                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name");
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name");

                return PartialView("_CreateEdit", new SysAttachScreenRole());
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_attach_screen_role", "CREATE_FORM_ERROR", "new", null, null,
                    $"Error loading create form: {ex.Message}");

                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new SysAttachScreenRole());
            }
        }

        // POST: create with rate limiting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SysAttachScreenRole model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("sys_attach_screen_role", "CREATE_ATTEMPT", recordId, null, model,
                    "Screen-role assignment creation attempt started");

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_attach_screen_role", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                    ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_attachscreenrole_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_attach_screen_role", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 screen-role assignments every 5 minutes. Please wait and try again.";
                    ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                    ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Check if this role already exists
                var exists = await _repo.ExistsRoleAsync(model.role_uid);
                if (exists)
                {
                    ModelState.AddModelError("", "This role is already assigned.");

                    // Log duplicate role attempt
                    await _auditService.LogAsync("sys_attach_screen_role", "CREATE_DUPLICATE_ROLE", recordId, null, model,
                        $"Attempted to create duplicate role assignment for role_uid: {model.role_uid}");

                    ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                    ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                    return PartialView("_CreateEdit", model);
                }

                // Set audit fields for creation
                var currentUser = GetCurrentUserName();
                var istDateTime = GetISTDateTime();

                model.CreatedBy = currentUser;
                model.CreatedOn = istDateTime;
                model.ModifiedBy = currentUser;
                model.ModifiedOn = istDateTime;

                // Save to database
                await _repo.AddAsync(model);
                recordId = model.uid.ToString();

                // Get role and screen names for better audit message
                var roleList = await _repo.GetRoleListAsync();
                var screenList = await _repo.GetScreenListAsync();
                var roleName = roleList.FirstOrDefault(r => r.role_id == model.role_uid)?.role_name ?? "Unknown";
                var screenNames = model.screen_uids?.Select(id =>
                    screenList.FirstOrDefault(s => s.screen_uid == id)?.screen_name ?? $"[ID:{id}]") ?? new List<string>();

                // Log successful creation
                await _auditService.LogCreateAsync("sys_attach_screen_role", recordId, model,
                    $"Screen-role assignment created: Role '{roleName}' assigned to screens [{string.Join(", ", screenNames)}]");

                return Json(new { success = true, message = "Screen-role assignment created successfully!", uid = model.uid });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("sys_attach_screen_role", "CREATE_FAILED", recordId, null, model,
                    $"Screen-role assignment creation failed: {ex.Message}");

                ViewBag.Error = "An error occurred while creating the screen-role assignment. Please try again.";
                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                return PartialView("_CreateEdit", model);
            }
        }

        // GET: edit form partial
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null)
                {
                    // Log not found attempt
                    await _auditService.LogAsync("sys_attach_screen_role", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent screen-role assignment with ID: {id}");

                    return NotFound();
                }

                // Get role name for better audit message
                var roleName = item.SysRole?.role_name ?? "Unknown";

                // Log edit form access
                await _auditService.LogViewAsync("sys_attach_screen_role", id.ToString(),
                    $"Edit form accessed for screen-role assignment: Role '{roleName}'");

                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", item.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", item.screen_uids);

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_attach_screen_role", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        // POST: edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SysAttachScreenRole model)
        {
            var recordId = model.uid.ToString();
            SysAttachScreenRole? oldAssignment = null;

            try
            {
                // Get the current assignment for audit comparison
                oldAssignment = await _repo.GetByIdWithBaseAsync(model.uid);
                if (oldAssignment == null)
                {
                    await _auditService.LogAsync("sys_attach_screen_role", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent screen-role assignment");

                    return NotFound();
                }

                // Get role name for better audit message
                var roleName = oldAssignment.SysRole?.role_name ?? "Unknown";

                // Log the update attempt
                await _auditService.LogAsync("sys_attach_screen_role", "UPDATE_ATTEMPT", recordId, oldAssignment, model,
                    $"Screen-role assignment update attempt for role: {roleName}");

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_attach_screen_role", "UPDATE_VALIDATION_FAILED", recordId, oldAssignment, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                    ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic for edit
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_attachscreenrole_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_attach_screen_role", "UPDATE_RATE_LIMITED", recordId, oldAssignment, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 screen-role assignments every 5 minutes. Please wait and try again.";
                    ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                    ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Get updated screen names for better audit message
                var screenList = await _repo.GetScreenListAsync();
                var screenNames = model.screen_uids?.Select(id =>
                    screenList.FirstOrDefault(s => s.screen_uid == id)?.screen_name ?? $"[ID:{id}]") ?? new List<string>();

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("sys_attach_screen_role", recordId, oldAssignment, model,
                    $"Screen-role assignment updated: Role '{roleName}' now assigned to screens [{string.Join(", ", screenNames)}]");

                return Json(new { success = true, message = "Screen-role assignment updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_attach_screen_role", "UPDATE_FAILED", recordId, oldAssignment, model,
                    $"Screen-role assignment update failed: {ex.Message}");

                ViewBag.Error = "An error occurred while updating the screen-role assignment. Please try again.";
                ViewBag.SysRoleList = new SelectList(await _repo.GetRoleListAsync(), "role_id", "role_name", model.role_uid);
                ViewBag.SysScreenList = new MultiSelectList(await _repo.GetScreenListAsync(), "screen_uid", "screen_name", model.screen_uids);
                return PartialView("_CreateEdit", model);
            }
        }

        // POST: delete
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            SysAttachScreenRole? assignmentToDelete = null;

            try
            {
                // Get entity before deletion for audit
                assignmentToDelete = await _repo.GetByIdWithBaseAsync(id);
                if (assignmentToDelete == null)
                {
                    await _auditService.LogAsync("sys_attach_screen_role", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent screen-role assignment with ID: {id}");

                    return Json(new { success = false, message = "Screen-role assignment not found." });
                }

                // Get role name for better audit message
                var roleName = assignmentToDelete.SysRole?.role_name ?? "Unknown";

                // Log deletion attempt
                await _auditService.LogAsync("sys_attach_screen_role", "DELETE_ATTEMPT", id.ToString(), assignmentToDelete, null,
                    $"Screen-role assignment deletion attempt for role: {roleName}");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("sys_attach_screen_role", id.ToString(), assignmentToDelete,
                    $"Screen-role assignment deleted: Role '{roleName}' assignment removed");

                return Json(new { success = true, message = "Screen-role assignment deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_attach_screen_role", "DELETE_FAILED", id.ToString(), assignmentToDelete, null,
                    $"Screen-role assignment deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the screen-role assignment." });
            }
        }

        // GET: /SysAttachScreenRole/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("sys_attach_screen_role", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent screen-role assignment with ID: {id}");

                    return NotFound();
                }

                var screenNames = await _repo.GetScreenNamesFromCsvAsync(item.screen_uid);
                ViewBag.ScreenNames = screenNames;

                // Get role name for better audit message
                var roleName = item.SysRole?.role_name ?? "Unknown";

                // Log details view
                await _auditService.LogViewAsync("sys_attach_screen_role", id.ToString(),
                    $"Screen-role assignment details viewed: Role '{roleName}'");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_attach_screen_role", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading screen-role assignment details: {ex.Message}");

                return NotFound();
            }
        }

        #region Private Methods for Audit Trail

        private string GetCurrentUserName()
        {
            // Try to get user name from different claims
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
            // Convert UTC to IST (UTC+5:30)
            var utcNow = DateTime.UtcNow;
            var istTimeZone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, istTimeZone);
        }

        #endregion
    }
}