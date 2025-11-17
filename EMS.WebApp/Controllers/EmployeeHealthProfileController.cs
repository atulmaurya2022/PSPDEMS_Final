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
    [Authorize("AccessEmployeeHealthProfile")]
    public class EmployeeHealthProfileController : Controller
    {
        private readonly IHealthProfileRepository _healthProfileRepository;
        private readonly ILogger<EmployeeHealthProfileController> _logger;
        private readonly ApplicationDbContext _db;

        public EmployeeHealthProfileController(
            IHealthProfileRepository healthProfileRepository,
            ILogger<EmployeeHealthProfileController> logger,
            ApplicationDbContext db)
        {
            _healthProfileRepository = healthProfileRepository;
            _logger = logger;
            _db = db;
        }

        // Helper method to get current user's plant ID
        private async Task<int?> GetCurrentUserPlantIdAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return null;

                return await _healthProfileRepository.GetUserPlantIdAsync(userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user plant ID");
                return null;
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Index accessed for plant: {userPlantId}");

                var model = new HealthProfileViewModel
                {
                    AvailableExamDates = await _healthProfileRepository.GetAvailableExamDatesAsync(userPlantId)
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Index page");
                return View(new HealthProfileViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeHealthForm(int empNo, DateTime? examDate = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Loading health form for employee {empNo}, exam date: {examDate}, Plant: {userPlantId}");

                var model = await _healthProfileRepository.LoadFormData(empNo, examDate, userPlantId);

                if (model == null)
                {
                    _logger.LogWarning($"Employee {empNo} not found or access denied for plant {userPlantId}");
                    return NotFound("Employee not found or access denied.");
                }

                // If no examDate was provided, this is a new entry with current system date
                if (!examDate.HasValue)
                {
                    model.ExamDate = DateTime.Now.Date;
                    model.IsNewEntry = true;
                }
                // If examDate was provided but no data exists, create new entry for that date
                else if (examDate.HasValue && model.IsNewEntry)
                {
                    model.ExamDate = examDate.Value.Date;
                }

                _logger.LogInformation($"Successfully loaded health form for employee {empNo}, Plant: {userPlantId}");
                return PartialView("_HealthFormPartial", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading employee health form for empNo: {empNo}");
                return BadRequest($"Error loading employee health form: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveHealthForm(HealthProfileViewModel model)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"SaveHealthForm called for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                if (!userPlantId.HasValue)
                {
                    _logger.LogWarning("User has no plant assigned");
                    return Json(new { success = false, message = "User is not assigned to any plant. Please contact administrator." });
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                    }

                    var reloadData = await _healthProfileRepository.LoadFormData(model.EmpNo, null, userPlantId);
                    if (reloadData != null)
                    {
                        // Retain edited values
                        reloadData.SelectedWorkAreaIds = model.SelectedWorkAreaIds ?? new List<int>();
                        reloadData.WorkHistories = model.WorkHistories ?? new List<MedWorkHistory>();
                        reloadData.GeneralExam = model.GeneralExam ?? new MedGeneralExam();
                        reloadData.ExamConditions = model.ExamConditions ?? new List<MedExamCondition>();
                        reloadData.FoodHabit = model.FoodHabit;
                        reloadData.ExamDate = model.ExamDate;
                        reloadData.SelectedConditionIds = model.SelectedConditionIds ?? new List<int>();

                        Response.StatusCode = 400;
                        return PartialView("_HealthFormPartial", reloadData);
                    }
                }

                // Set the plant ID for the health profile
                model.PlantId = (short)userPlantId.Value;

                await _healthProfileRepository.SaveFormDataAsync(model, userPlantId);
                _logger.LogInformation($"Successfully saved health form for EmpNo: {model.EmpNo}, Plant: {userPlantId}");

                return Json(new { success = true, message = "Health profile saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving employee health form for EmpNo: {model.EmpNo}");
                return Json(new { success = false, message = "An error occurred while saving." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableExamDates(int? empNo = null)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                _logger.LogInformation($"Getting available exam dates for employee {empNo}, Plant: {userPlantId}");

                List<DateTime> dates;

                if (empNo.HasValue)
                {
                    dates = await _healthProfileRepository.GetAvailableExamDatesAsync(empNo.Value, userPlantId);
                }
                else
                {
                    dates = await _healthProfileRepository.GetAvailableExamDatesAsync(userPlantId);
                }

                _logger.LogInformation($"Found {dates.Count} exam dates for plant {userPlantId}");
                return Json(dates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting exam dates for empNo: {empNo}");
                return Json(new List<DateTime>());
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

                // UPDATED: Now searches by emp_id instead of emp_uid and includes plant filtering
                var employeeIds = await _healthProfileRepository.GetMatchingEmployeeIdsAsync(term, userPlantId);

                _logger.LogInformation($"Found {employeeIds.Count} matching employee IDs for plant {userPlantId}");
                return Json(employeeIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching employee IDs with term: {term}");
                return Json(new List<string>());
            }
        }

        // NEW: Method to get current user's plant information
        [HttpGet]
        public async Task<IActionResult> GetCurrentUserPlant()
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();

                if (!userPlantId.HasValue)
                {
                    return Json(new { success = false, message = "No plant assigned" });
                }

                var plant = await _db.org_plants.FindAsync(userPlantId.Value);
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
                    return Json(new { success = false, message = "Employee not found or access denied." });
                }

                _logger.LogInformation($"Successfully converted Employee ID {empId} to UID {employee.emp_uid}");
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
                return Json(new { success = false, message = "Error finding employee." });
            }
        }
    }
}