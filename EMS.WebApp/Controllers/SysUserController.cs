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
    [Authorize("AccessSysUser")]
    public class SysUserController : Controller
    {
        private readonly ISysUserRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        // Allowed email domains
        private readonly string[] _allowedEmailDomains = { "itc.in", "associatemail.in" };

        public SysUserController(ISysUserRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        // GET: /SysUser
        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var currentUserName = User.Identity?.Name;
                var currentUserRole = User.FindFirst("RoleName")?.Value ?? "";

                // DEBUG: Log the current user info
                Console.WriteLine($"DEBUG - Current User: {currentUserName}");
                Console.WriteLine($"DEBUG - Current User Role: {currentUserRole}");

                // Check if current user is admin
                var isAdmin = await _repo.IsAdminRoleAsync(currentUserRole);

                // DEBUG: Log admin check result
                Console.WriteLine($"DEBUG - Is Admin: {isAdmin}");

                IEnumerable<SysUser> list;

                if (isAdmin)
                {
                    // Admin can see all users
                    list = await _repo.ListWithBaseAsync();
                    Console.WriteLine($"DEBUG - Admin loading all users: {list.Count()}");
                }
                else
                {
                    // Non-admin users can only see users from their plant
                    var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);
                    Console.WriteLine($"DEBUG - User Plant ID: {userPlantId}");

                    if (userPlantId.HasValue)
                    {
                        list = await _repo.ListWithBaseByPlantAsync(userPlantId.Value);
                        Console.WriteLine($"DEBUG - Non-admin loading plant users: {list.Count()}");
                    }
                    else
                    {
                        // If user's plant is not found, show empty list
                        list = new List<SysUser>();
                        Console.WriteLine($"DEBUG - No plant found, empty list");
                    }
                }

                var result = list.Select(x => new {
                    x.user_id,
                    x.adid,
                    role_name = x.SysRole != null ? x.SysRole.role_name : "",
                    plant_name = x.OrgPlant != null ? x.OrgPlant.plant_name : "",
                    x.full_name,
                    x.email,
                    x.is_active,
                    x.CreatedBy,
                    x.CreatedOn,
                    x.ModifiedBy,
                    x.ModifiedOn
                });

                // Log data access for security monitoring
                await _auditService.LogAsync("sys_user", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} users for listing (Admin: {isAdmin})");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG - Error in LoadData: {ex.Message}");

                // Log the error
                await _auditService.LogAsync("sys_user", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load users: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }
        // GET: create form partial
        public async Task<IActionResult> Create()
        {
            try
            {
                // Log form access
                _ = Task.Run(async () => await _auditService.LogAsync("sys_user", "CREATE_FORM_VIEW", "new", null, null,
                    "Create user form accessed"));

                var roleList = await _repo.GetBaseListAsync();
                var plantList = await _repo.GetPlantListAsync();

                if (!roleList.Any())
                {
                    ViewBag.SysRoleList = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.Error = "⚠ No roles found! Please create roles first.";
                }
                else
                {
                    ViewBag.SysRoleList = new SelectList(roleList, "role_id", "role_name");
                }

                if (!plantList.Any())
                {
                    ViewBag.PlantMasterList = new SelectList(Enumerable.Empty<SelectListItem>());
                    if (string.IsNullOrEmpty(ViewBag.Error as string))
                        ViewBag.Error = "⚠ No plants found! Please create plants first.";
                    else
                        ViewBag.Error += " Also, no plants found!";
                }
                else
                {
                    ViewBag.PlantMasterList = new SelectList(plantList, "plant_id", "plant_name");
                }

                return PartialView("_CreateEdit", new SysUser());
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_user", "CREATE_FORM_ERROR", "new", null, null,
                    $"Error loading create form: {ex.Message}");

                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new SysUser());
            }
        }

        // POST: create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SysUser model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("sys_user", "CREATE_ATTEMPT", recordId, null, model,
                    "User creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("sys_user", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during user creation");

                    await LoadDropdownLists(model.role_id, model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Validate email domain
                if (!IsEmailDomainValid(model.email))
                {
                    ModelState.AddModelError("email", $"Email domain must be one of: {string.Join(", ", _allowedEmailDomains.Select(d => "@" + d))}");

                    // Log invalid domain attempt
                    await _auditService.LogAsync("sys_user", "CREATE_INVALID_DOMAIN", recordId, null, model,
                        $"Invalid email domain attempted: {model.email}");
                }

                // Check for duplicate email
                if (await IsEmailExistsAsync(model.email))
                {
                    ModelState.AddModelError("email", "A user with this email already exists. Please use a different email.");

                    // Log duplicate email attempt
                    await _auditService.LogAsync("sys_user", "CREATE_DUPLICATE_EMAIL", recordId, null, model,
                        $"Attempted to create duplicate email: {model.email}");
                }

                // Check for duplicate ADID if provided
                if (!string.IsNullOrEmpty(model.adid) && await IsAdidExistsAsync(model.adid))
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists. Please use a different ADID.");

                    // Log duplicate ADID attempt
                    await _auditService.LogAsync("sys_user", "CREATE_DUPLICATE_ADID", recordId, null, model,
                        $"Attempted to create duplicate ADID: {model.adid}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_user", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    await LoadDropdownLists(model.role_id, model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_sysuser_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_user", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    await LoadDropdownLists(model.role_id, model.plant_id);
                    ViewBag.Error = "⚠ You can only create 5 users every 5 minutes. Please wait and try again.";
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
                recordId = model.user_id.ToString();

                // Log successful creation
                await _auditService.LogCreateAsync("sys_user", recordId, model,
                    $"User '{model.full_name}' ({model.email}) created successfully");

                return Json(new { success = true, message = "User created successfully!", userId = model.user_id });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("sys_user", "CREATE_FAILED", recordId, null, model,
                    $"User creation failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("email") == true)
                {
                    ModelState.AddModelError("email", "A user with this email already exists.");
                }
                else if (ex.InnerException?.Message.Contains("adid") == true)
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while creating the user. Please try again.";
                }

                await LoadDropdownLists(model.role_id, model.plant_id);
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
                    await _auditService.LogAsync("sys_user", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent user with ID: {id}");

                    return NotFound();
                }

                // Check if current user can edit this user (plant-based authorization)
                var currentUserName = User.Identity?.Name;
                var currentUserRole = User.FindFirst("RoleName")?.Value ?? "";
                var isAdmin = await _repo.IsAdminRoleAsync(currentUserRole);

                if (!isAdmin)
                {
                    var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);
                    if (!userPlantId.HasValue || userPlantId.Value != item.plant_id)
                    {
                        await _auditService.LogAsync("sys_user", "EDIT_UNAUTHORIZED", id.ToString(), null, null,
                            $"Unauthorized edit attempt for user ID: {id}");
                        return Forbid();
                    }
                }

                // Log edit form access
                await _auditService.LogViewAsync("sys_user", id.ToString(),
                    $"Edit form accessed for user: {item.full_name} ({item.email})");

                await LoadDropdownLists(item.role_id, item.plant_id);
                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_user", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        // POST: edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SysUser model)
        {
            var recordId = model.user_id.ToString();
            SysUser? oldUser = null;

            try
            {
                // Get the current user for audit comparison
                oldUser = await _repo.GetByIdWithBaseAsync(model.user_id);
                if (oldUser == null)
                {
                    await _auditService.LogAsync("sys_user", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent user");

                    return NotFound();
                }

                // Check if current user can edit this user (plant-based authorization)
                var currentUserName = User.Identity?.Name;
                var currentUserRole = User.FindFirst("RoleName")?.Value ?? "";
                var isAdmin = await _repo.IsAdminRoleAsync(currentUserRole);

                if (!isAdmin)
                {
                    var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);
                    if (!userPlantId.HasValue || userPlantId.Value != oldUser.plant_id)
                    {
                        await _auditService.LogAsync("sys_user", "EDIT_UNAUTHORIZED", recordId, oldUser, model,
                            $"Unauthorized edit attempt for user ID: {model.user_id}");
                        return Forbid();
                    }
                }

                // Log the update attempt
                await _auditService.LogAsync("sys_user", "UPDATE_ATTEMPT", recordId, oldUser, model,
                    $"User update attempt for: {oldUser.full_name} ({oldUser.email})");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("sys_user", "UPDATE_SECURITY_VIOLATION", recordId, oldUser, model,
                        "Insecure input detected during user update");

                    await LoadDropdownLists(model.role_id, model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Validate email domain
                if (!IsEmailDomainValid(model.email))
                {
                    ModelState.AddModelError("email", $"Email domain must be one of: {string.Join(", ", _allowedEmailDomains.Select(d => "@" + d))}");

                    // Log invalid domain attempt
                    await _auditService.LogAsync("sys_user", "UPDATE_INVALID_DOMAIN", recordId, oldUser, model,
                        $"Invalid email domain attempted: {model.email}");
                }

                // Check for duplicate email (excluding current user)
                if (await IsEmailExistsAsync(model.email, model.user_id))
                {
                    ModelState.AddModelError("email", "A user with this email already exists. Please use a different email.");

                    // Log duplicate email attempt
                    await _auditService.LogAsync("sys_user", "UPDATE_DUPLICATE_EMAIL", recordId, oldUser, model,
                        $"Attempted to update to duplicate email: {model.email}");
                }

                // Check for duplicate ADID if provided (excluding current user)
                if (!string.IsNullOrEmpty(model.adid) && await IsAdidExistsAsync(model.adid, model.user_id))
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists. Please use a different ADID.");

                    // Log duplicate ADID attempt
                    await _auditService.LogAsync("sys_user", "UPDATE_DUPLICATE_ADID", recordId, oldUser, model,
                        $"Attempted to update to duplicate ADID: {model.adid}");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("sys_user", "UPDATE_VALIDATION_FAILED", recordId, oldUser, model,
                        $"Validation failed: {validationErrors}");

                    await LoadDropdownLists(model.role_id, model.plant_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_sysuser_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("sys_user", "UPDATE_RATE_LIMITED", recordId, oldUser, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    await LoadDropdownLists(model.role_id, model.plant_id);
                    ViewBag.Error = "⚠ You can only edit 10 users every 5 minutes. Please wait and try again.";
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("sys_user", recordId, oldUser, model,
                    $"User '{model.full_name}' ({model.email}) updated successfully");

                return Json(new { success = true, message = "User updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_user", "UPDATE_FAILED", recordId, oldUser, model,
                    $"User update failed: {ex.Message}");

                // Handle database constraint violations
                if (ex.InnerException?.Message.Contains("email") == true)
                {
                    ModelState.AddModelError("email", "A user with this email already exists.");
                }
                else if (ex.InnerException?.Message.Contains("adid") == true)
                {
                    ModelState.AddModelError("adid", "A user with this ADID already exists.");
                }
                else
                {
                    ViewBag.Error = "An error occurred while updating the user. Please try again.";
                }

                await LoadDropdownLists(model.role_id, model.plant_id);
                return PartialView("_CreateEdit", model);
            }
        }

        // POST: delete
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            SysUser? userToDelete = null;

            try
            {
                // Get entity before deletion for audit
                userToDelete = await _repo.GetByIdWithBaseAsync(id);
                if (userToDelete == null)
                {
                    await _auditService.LogAsync("sys_user", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent user with ID: {id}");

                    return Json(new { success = false, message = "User not found." });
                }

                // Check if current user can delete this user (plant-based authorization)
                var currentUserName = User.Identity?.Name;
                var currentUserRole = User.FindFirst("RoleName")?.Value ?? "";
                var isAdmin = await _repo.IsAdminRoleAsync(currentUserRole);

                if (!isAdmin)
                {
                    var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);
                    if (!userPlantId.HasValue || userPlantId.Value != userToDelete.plant_id)
                    {
                        await _auditService.LogAsync("sys_user", "DELETE_UNAUTHORIZED", id.ToString(), userToDelete, null,
                            $"Unauthorized delete attempt for user ID: {id}");
                        return Json(new { success = false, message = "You don't have permission to delete this user." });
                    }
                }

                // Log deletion attempt
                await _auditService.LogAsync("sys_user", "DELETE_ATTEMPT", id.ToString(), userToDelete, null,
                    $"User deletion attempt for: {userToDelete.full_name} ({userToDelete.email})");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("sys_user", id.ToString(), userToDelete,
                    $"User '{userToDelete.full_name}' ({userToDelete.email}) deleted successfully");

                return Json(new { success = true, message = "User deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("sys_user", "DELETE_FAILED", id.ToString(), userToDelete, null,
                    $"User deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the user." });
            }
        }

        // GET: /SysUser/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("sys_user", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent user with ID: {id}");

                    return NotFound();
                }

                // Check if current user can view this user (plant-based authorization)
                var currentUserName = User.Identity?.Name;
                var currentUserRole = User.FindFirst("RoleName")?.Value ?? "";
                var isAdmin = await _repo.IsAdminRoleAsync(currentUserRole);

                if (!isAdmin)
                {
                    var userPlantId = await _repo.GetUserPlantIdAsync(currentUserName);
                    if (!userPlantId.HasValue || userPlantId.Value != item.plant_id)
                    {
                        await _auditService.LogAsync("sys_user", "DETAILS_UNAUTHORIZED", id.ToString(), null, null,
                            $"Unauthorized details view attempt for user ID: {id}");
                        return Forbid();
                    }
                }

                // Log details view
                await _auditService.LogViewAsync("sys_user", id.ToString(),
                    $"User details viewed: {item.full_name} ({item.email})");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("sys_user", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading user details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX methods for real-time validation
        [HttpPost]
        public async Task<IActionResult> CheckEmailExists(string email, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { exists = false });

            // Sanitize input before checking
            email = SanitizeString(email);

            // Also check domain validity
            if (!IsEmailDomainValid(email))
                return Json(new { exists = false, invalidDomain = true });

            var exists = await IsEmailExistsAsync(email, userId);
            return Json(new { exists = exists });
        }

        [HttpPost]
        public async Task<IActionResult> CheckAdidExists(string adid, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(adid))
                return Json(new { exists = false });

            // Sanitize input before checking
            adid = SanitizeString(adid);

            var exists = await IsAdidExistsAsync(adid, userId);
            return Json(new { exists = exists });
        }

        #region Private Helper Methods

        private async Task LoadDropdownLists(int? selectedRoleId = null, int? selectedPlantId = null)
        {
            var roleList = await _repo.GetBaseListAsync();
            var plantList = await _repo.GetPlantListAsync();

            ViewBag.SysRoleList = new SelectList(roleList, "role_id", "role_name", selectedRoleId);
            ViewBag.PlantMasterList = new SelectList(plantList, "plant_id", "plant_name", selectedPlantId);
        }

        private SysUser SanitizeInput(SysUser model)
        {
            if (model == null) return model;

            model.adid = SanitizeString(model.adid);
            model.full_name = SanitizeString(model.full_name);
            model.email = SanitizeString(model.email);

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

        private bool IsInputSecure(SysUser model)
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

            var inputsToCheck = new[] { model.adid, model.full_name, model.email };

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

        private bool IsEmailDomainValid(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Basic email format check
                if (!email.Contains('@'))
                    return false;

                var domain = email.Split('@')[1].ToLower();
                return _allowedEmailDomains.Contains(domain);
            }
            catch
            {
                return false;
            }
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

        private async Task<bool> IsEmailExistsAsync(string email, int? excludeUserId = null)
        {
            try
            {
                var users = await _repo.ListAsync();
                var query = users.Where(u => u.email.ToLower() == email.ToLower());

                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.user_id != excludeUserId.Value);
                }

                return query.Any();
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsAdidExistsAsync(string adid, int? excludeUserId = null)
        {
            try
            {
                var users = await _repo.ListAsync();
                var query = users.Where(u => !string.IsNullOrEmpty(u.adid) && u.adid.ToLower() == adid.ToLower());

                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.user_id != excludeUserId.Value);
                }

                return query.Any();
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}