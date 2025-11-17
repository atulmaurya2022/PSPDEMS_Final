using EMS.WebApp.Data;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Web;
using EMS.WebApp.Extensions;

namespace EMS.WebApp.Controllers
{
    [Authorize("AccessMedExamCategory")]
    public class MedExamCategoryController : Controller
    {
        private readonly IMedExamCategoryRepository _repo;
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;

        public MedExamCategoryController(IMedExamCategoryRepository repo, IMemoryCache cache, IAuditService auditService)
        {
            _repo = repo;
            _cache = cache;
            _auditService = auditService;
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> LoadData()
        {
            try
            {
                var list = await _repo.ListAsync();

                var result = list.Select(x => new
                {
                    x.CatId,
                    x.CatName,
                    x.YearsFreq,
                    x.AnnuallyRule,
                    x.MonthsSched,
                    criteria_name = x.med_criteria != null ? x.med_criteria.criteria_name : "N/A", // New field
                    x.Remarks,
                    x.CreatedBy,
                    x.CreatedOn,
                    x.ModifiedBy,
                    x.ModifiedOn
                });

                // Log data access for security monitoring
                await _auditService.LogAsync("med_exam_category", "LOAD_DATA", "multiple", null, null,
                    $"Loaded {list.Count()} exam category records for listing");

                return Json(new { data = result });
            }
            catch (Exception ex)
            {
                // Log the error
                await _auditService.LogAsync("med_exam_category", "LOAD_DATA_FAILED", "multiple", null, null,
                    $"Failed to load exam category records: {ex.Message}");

                return Json(new { data = new List<object>(), error = "Error loading data." });
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                // Log form access
                _ = Task.Run(async () => await _auditService.LogAsync("med_exam_category", "CREATE_FORM_VIEW", "new", null, null,
                    "Create exam category record form accessed"));

                var medCriteriaList = await _repo.GetMedCriteriaListAsync();
                ViewBag.MedCriteriaList = new SelectList(medCriteriaList, "criteria_id", "criteria_name");

                return PartialView("_CreateEdit", new MedExamCategory());
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_exam_category", "CREATE_FORM_ERROR", "new", null, null,
                    $"Error loading create form: {ex.Message}");

                ViewBag.Error = "Error loading create form.";
                return PartialView("_CreateEdit", new MedExamCategory());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedExamCategory model)
        {
            string recordId = "new";

            try
            {
                // Log the creation attempt
                await _auditService.LogAsync("med_exam_category", "CREATE_ATTEMPT", recordId, null, model,
                    "Exam category record creation attempt started");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_exam_category", "CREATE_SECURITY_VIOLATION", recordId, null, model,
                        "Insecure input detected during exam category record creation");

                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate category details combination (now includes criteria_id)
                if (await _repo.IsCategoryDetailsExistsAsync(model.CatName, model.YearsFreq, model.AnnuallyRule, model.MonthsSched, model.criteria_id))
                {
                    ModelState.AddModelError("", "A category with this combination of name, years frequency, annually rule, months schedule, and medical criteria already exists. Please choose different values.");
                    // Add specific field errors for better UX
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                    ModelState.AddModelError("criteria_id", "This combination already exists.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_exam_category", "CREATE_DUPLICATE_ATTEMPT", recordId, null, model,
                        $"Attempted to create duplicate exam category: {model.CatName} (YearsFreq: {model.YearsFreq}, AnnuallyRule: {model.AnnuallyRule}, MonthsSched: {model.MonthsSched}, CriteriaId: {model.criteria_id})");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_exam_category", "CREATE_VALIDATION_FAILED", recordId, null, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_create_medexamcategory_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 5)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_exam_category", "CREATE_RATE_LIMITED", recordId, null, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only create 5 MedExamCategory records every 5 minutes. Please wait and try again.";
                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
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
                recordId = model.CatId.ToString();

                // Get medical criteria name for better audit message
                var medCriteriaList = await _repo.GetMedCriteriaListAsync();
                var criteriaName = medCriteriaList.FirstOrDefault(c => c.criteria_id == model.criteria_id)?.criteria_name ?? "Unknown";

                // Log successful creation
                await _auditService.LogCreateAsync("med_exam_category", recordId, model,
                    $"Exam category '{model.CatName}' (YearsFreq: {model.YearsFreq}, AnnuallyRule: {model.AnnuallyRule}, Criteria: {criteriaName}) created successfully");

                return Json(new { success = true, message = "Exam category created successfully!", catId = model.CatId });
            }
            catch (Exception ex)
            {
                // Log the failed attempt with full error details
                await _auditService.LogAsync("med_exam_category", "CREATE_FAILED", recordId, null, model,
                    $"Exam category record creation failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSchedCriteria_Unique") == true)
                {
                    ModelState.AddModelError("", "A category with this combination already exists. Please choose different values.");
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                    ModelState.AddModelError("criteria_id", "This combination already exists.");
                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while creating the exam category. Please try again.";
                ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                return PartialView("_CreateEdit", model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null)
                {
                    // Log not found attempt
                    await _auditService.LogAsync("med_exam_category", "EDIT_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to edit non-existent exam category record with ID: {id}");

                    return NotFound();
                }

                // Log edit form access
                var criteriaName = item.med_criteria?.criteria_name ?? "Unknown";
                await _auditService.LogViewAsync("med_exam_category", id.ToString(),
                    $"Edit form accessed for exam category: {item.CatName} (YearsFreq: {item.YearsFreq}, AnnuallyRule: {item.AnnuallyRule}, Criteria: {criteriaName})");

                ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", item.criteria_id);

                return PartialView("_CreateEdit", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_exam_category", "EDIT_FORM_ERROR", id.ToString(), null, null,
                    $"Error loading edit form: {ex.Message}");

                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MedExamCategory model)
        {
            var recordId = model.CatId.ToString();
            MedExamCategory? oldCategory = null;

            try
            {
                // Get the current category for audit comparison
                oldCategory = await _repo.GetByIdWithBaseAsync(model.CatId);
                if (oldCategory == null)
                {
                    await _auditService.LogAsync("med_exam_category", "EDIT_NOT_FOUND", recordId, null, model,
                        "Attempted to edit non-existent exam category record");

                    return NotFound();
                }

                // Log the update attempt
                var oldCriteriaName = oldCategory.med_criteria?.criteria_name ?? "Unknown";
                await _auditService.LogAsync("med_exam_category", "UPDATE_ATTEMPT", recordId, oldCategory, model,
                    $"Exam category record update attempt for: {oldCategory.CatName} (YearsFreq: {oldCategory.YearsFreq}, AnnuallyRule: {oldCategory.AnnuallyRule}, Criteria: {oldCriteriaName})");

                // Sanitize input before processing
                model = SanitizeInput(model);

                // Additional security validation
                if (!IsInputSecure(model))
                {
                    ModelState.AddModelError("", "Invalid input detected. Please remove any script tags or unsafe characters.");

                    // Log security violation
                    await _auditService.LogAsync("med_exam_category", "UPDATE_SECURITY_VIOLATION", recordId, oldCategory, model,
                        "Insecure input detected during exam category record update");

                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                // Check for duplicate category details combination (excluding current record, now includes criteria_id)
                if (await _repo.IsCategoryDetailsExistsAsync(model.CatName, model.YearsFreq, model.AnnuallyRule, model.MonthsSched, model.criteria_id, model.CatId))
                {
                    ModelState.AddModelError("", "A category with this combination of name, years frequency, annually rule, months schedule, and medical criteria already exists. Please choose different values.");
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                    ModelState.AddModelError("criteria_id", "This combination already exists.");

                    // Log duplicate attempt
                    await _auditService.LogAsync("med_exam_category", "UPDATE_DUPLICATE_ATTEMPT", recordId, oldCategory, model,
                        $"Attempted to update to duplicate exam category: {model.CatName} (YearsFreq: {model.YearsFreq}, AnnuallyRule: {model.AnnuallyRule}, MonthsSched: {model.MonthsSched}, CriteriaId: {model.criteria_id})");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation failure
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    await _auditService.LogAsync("med_exam_category", "UPDATE_VALIDATION_FAILED", recordId, oldCategory, model,
                        $"Validation failed: {validationErrors}");

                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                // Rate limiting logic
                var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
                var cacheKey = $"rate_limit_edit_medexamcategory_{userId}";

                var timestamps = _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    return new List<DateTime>();
                });

                timestamps.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));

                if (timestamps.Count >= 10)
                {
                    // Log rate limit violation
                    await _auditService.LogAsync("med_exam_category", "UPDATE_RATE_LIMITED", recordId, oldCategory, model,
                        $"Rate limit exceeded: {timestamps.Count} attempts in 5 minutes");

                    ViewBag.Error = "⚠ You can only edit 10 MedExamCategory records every 5 minutes. Please wait and try again.";
                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                timestamps.Add(DateTime.UtcNow);
                _cache.Set(cacheKey, timestamps, TimeSpan.FromMinutes(5));

                // Update with audit fields preservation
                await _repo.UpdateAsync(model, GetCurrentUserName(), GetISTDateTime());

                // Get updated medical criteria name for better audit message
                var medCriteriaList = await _repo.GetMedCriteriaListAsync();
                var updatedCriteriaName = medCriteriaList.FirstOrDefault(c => c.criteria_id == model.criteria_id)?.criteria_name ?? "Unknown";

                // Log successful update with comparison
                await _auditService.LogUpdateAsync("med_exam_category", recordId, oldCategory, model,
                    $"Exam category '{model.CatName}' (YearsFreq: {model.YearsFreq}, AnnuallyRule: {model.AnnuallyRule}, Criteria: {updatedCriteriaName}) updated successfully");

                return Json(new { success = true, message = "Exam category updated successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_exam_category", "UPDATE_ERROR", recordId, oldCategory, model,
                    $"Exam category record update failed: {ex.Message}");

                // Handle database constraint violation
                if (ex.InnerException?.Message.Contains("IX_MedExamCategory_CatNameYearsFreqAnnuallyRuleMonthsSchedCriteria_Unique") == true)
                {
                    ModelState.AddModelError("", "A category with this combination already exists. Please choose different values.");
                    ModelState.AddModelError("CatName", "This combination already exists.");
                    ModelState.AddModelError("YearsFreq", "This combination already exists.");
                    ModelState.AddModelError("AnnuallyRule", "This combination already exists.");
                    ModelState.AddModelError("MonthsSched", "This combination already exists.");
                    ModelState.AddModelError("criteria_id", "This combination already exists.");
                    ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                    return PartialView("_CreateEdit", model);
                }

                // Log the error and return a generic error message
                ViewBag.Error = "An error occurred while updating the exam category. Please try again.";
                ViewBag.MedCriteriaList = new SelectList(await _repo.GetMedCriteriaListAsync(), "criteria_id", "criteria_name", model.criteria_id);
                return PartialView("_CreateEdit", model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            MedExamCategory? categoryToDelete = null;

            try
            {
                // Get entity before deletion for audit
                categoryToDelete = await _repo.GetByIdWithBaseAsync(id);
                if (categoryToDelete == null)
                {
                    await _auditService.LogAsync("med_exam_category", "DELETE_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to delete non-existent exam category record with ID: {id}");

                    return Json(new { success = false, message = "Exam category record not found." });
                }

                // Log deletion attempt
                var criteriaName = categoryToDelete.med_criteria?.criteria_name ?? "Unknown";
                await _auditService.LogAsync("med_exam_category", "DELETE_ATTEMPT", id.ToString(), categoryToDelete, null,
                    $"Exam category record deletion attempt for: {categoryToDelete.CatName} (YearsFreq: {categoryToDelete.YearsFreq}, AnnuallyRule: {categoryToDelete.AnnuallyRule}, Criteria: {criteriaName})");

                await _repo.DeleteAsync(id);

                // Log successful deletion
                await _auditService.LogDeleteAsync("med_exam_category", id.ToString(), categoryToDelete,
                    $"Exam category '{categoryToDelete.CatName}' (YearsFreq: {categoryToDelete.YearsFreq}, AnnuallyRule: {categoryToDelete.AnnuallyRule}, Criteria: {criteriaName}) deleted successfully");

                return Json(new { success = true, message = "Exam category deleted successfully!" });
            }
            catch (Exception ex)
            {
                // Log the failed attempt
                await _auditService.LogAsync("med_exam_category", "DELETE_FAILED", id.ToString(), categoryToDelete, null,
                    $"Exam category record deletion failed: {ex.Message}");

                return Json(new { success = false, message = "An error occurred while deleting the exam category." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var item = await _repo.GetByIdWithBaseAsync(id);
                if (item == null)
                {
                    await _auditService.LogAsync("med_exam_category", "DETAILS_NOT_FOUND", id.ToString(), null, null,
                        $"Attempted to view details of non-existent exam category record with ID: {id}");

                    return NotFound();
                }

                // Log details view
                var criteriaName = item.med_criteria?.criteria_name ?? "Unknown";
                await _auditService.LogViewAsync("med_exam_category", id.ToString(),
                    $"Exam category record details viewed: {item.CatName} (YearsFreq: {item.YearsFreq}, AnnuallyRule: {item.AnnuallyRule}, Criteria: {criteriaName})");

                return PartialView("_View", item);
            }
            catch (Exception ex)
            {
                await _auditService.LogAsync("med_exam_category", "DETAILS_VIEW_ERROR", id.ToString(), null, null,
                    $"Error loading exam category record details: {ex.Message}");

                return NotFound();
            }
        }

        // AJAX method for real-time validation (updated to include criteria_id)
        [HttpPost]
        public async Task<IActionResult> CheckCategoryDetailsExists(string catName, byte yearsFreq, string annuallyRule, string monthsSched, short criteriaId, int? catId = null)
        {
            if (string.IsNullOrWhiteSpace(catName) || string.IsNullOrWhiteSpace(annuallyRule) || string.IsNullOrWhiteSpace(monthsSched) || criteriaId <= 0)
                return Json(new { exists = false });

            // Sanitize inputs before checking
            catName = SanitizeString(catName);
            annuallyRule = SanitizeString(annuallyRule);
            monthsSched = SanitizeString(monthsSched);

            var exists = await _repo.IsCategoryDetailsExistsAsync(catName, yearsFreq, annuallyRule, monthsSched, criteriaId, catId);
            return Json(new { exists = exists });
        }

        #region Private Methods for Input Sanitization and Validation

        private MedExamCategory SanitizeInput(MedExamCategory model)
        {
            if (model == null) return model;

            model.CatName = SanitizeString(model.CatName);
            model.AnnuallyRule = SanitizeString(model.AnnuallyRule);
            model.MonthsSched = SanitizeString(model.MonthsSched);
            model.Remarks = SanitizeString(model.Remarks);

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

        private bool IsInputSecure(MedExamCategory model)
        {
            if (model == null) return false;

            // Check for potentially dangerous patterns only in text fields
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

            // Only check text inputs that can contain user-entered content
            var inputsToCheck = new[] { model.CatName, model.Remarks };

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

            // Additional validation for controlled inputs (dropdown and checkboxes)
            if (!string.IsNullOrEmpty(model.AnnuallyRule))
            {
                if (!new[] { "once", "twice", "thrice" }.Contains(model.AnnuallyRule.ToLower()))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(model.MonthsSched))
            {
                var validMonths = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                var selectedMonths = model.MonthsSched.Split(',');

                foreach (var month in selectedMonths)
                {
                    if (!validMonths.Contains(month.Trim()))
                    {
                        return false;
                    }
                }

                // Validate selection count matches annually rule
                var maxAllowed = model.AnnuallyRule?.ToLower() switch
                {
                    "once" => 1,
                    "twice" => 2,
                    "thrice" => 3,
                    _ => 0
                };

                if (selectedMonths.Length != maxAllowed)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

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
    }
}