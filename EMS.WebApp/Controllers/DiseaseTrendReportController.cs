using EMS.WebApp.Data;
using EMS.WebApp.Extensions;
using EMS.WebApp.Models;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Controllers
{
    [Authorize]
    public class DiseaseTrendReportController : Controller
    {
        private readonly IDiseaseTrendRepository _repository;
        private readonly ILogger<DiseaseTrendReportController> _logger;

        public DiseaseTrendReportController(
            IDiseaseTrendRepository repository,
            ILogger<DiseaseTrendReportController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        #region Index

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        #endregion

        #region Helper Methods

        private async Task<int?> GetCurrentUserPlantIdAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return null;
            return await _repository.GetUserPlantIdAsync(userName);
        }

        /// <summary>
        /// ✅ FIX: CurrentUser now uses "ADID - FullName" format to match CreatedBy stored in prescriptions.
        /// Mirrors the format used in CompounderIndentController.
        /// </summary>
        private string GetCurrentUserName()
        {
            return (User.Identity?.Name + " - " + User.GetFullName()).Trim(' ', '-');
        }

        /// <summary>
        /// ✅ FIX: Reads user role from SysUsers → SysRole.role_name.
        /// Exactly mirrors GetUserRoleAsync() in CompounderIndentController.
        /// </summary>
        private async Task<string?> GetUserRoleAsync()
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName)) return null;

                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var user = await dbContext.SysUsers
                    .Include(u => u.SysRole)
                    .FirstOrDefaultAsync(u => u.full_name == userName || u.email == userName || u.adid == userName);

                return user?.SysRole?.role_name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role for Disease Trend reports");
                return null;
            }
        }

        /// <summary>
        /// ✅ FIX: Doctor and Admin see all records; Compounder/Store see only their own.
        /// </summary>
        private bool IsDocterOrAdmin(string? userRole)
        {
            if (string.IsNullOrEmpty(userRole)) return false;
            var role = userRole.ToLower();
            return role == "doctor" || role.Contains("admin");
        }

        /// <summary>
        /// ✅ FIX: Parses comma-separated disease IDs from the multi-select control.
        /// e.g. "3,7,12" → List{3, 7, 12}
        /// </summary>
        private static List<int>? ParseDiseaseIds(string? diseaseIds)
        {
            if (string.IsNullOrWhiteSpace(diseaseIds)) return null;

            var ids = new List<int>();
            foreach (var part in diseaseIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var id))
                    ids.Add(id);
            }
            return ids.Any() ? ids : null;
        }

        #endregion

        #region Age Wise Report

        [HttpGet]
        public async Task<IActionResult> DiseaseTrendAgeWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAgeWiseData(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            string? diseaseIds,           // ✅ FIX: was int? diseaseId — now string, parsed below
            int? employeeCategoryId)      // ✅ FIX: was string? employeeType — now int FK
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseIds = ParseDiseaseIds(diseaseIds),   // ✅ FIX
                    EmployeeCategoryId = employeeCategoryId     // ✅ FIX
                };

                var result = await _repository.GetDiseaseTrendAgeWiseAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting age wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportAgeWise(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            string? diseaseIds,
            int? employeeCategoryId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseIds = ParseDiseaseIds(diseaseIds),
                    EmployeeCategoryId = employeeCategoryId
                };

                var fileBytes = await _repository.ExportAgeWiseToExcelAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                var fileName = $"DiseaseTrend_AgeWise_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting age wise report");
                TempData["Error"] = "Failed to export report: " + ex.Message;
                return RedirectToAction(nameof(DiseaseTrendAgeWise));
            }
        }

        #endregion

        #region Department Wise Report

        [HttpGet]
        public async Task<IActionResult> DiseaseTrendDeptWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDeptWiseData(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            string? diseaseIds,
            int? employeeCategoryId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseIds = ParseDiseaseIds(diseaseIds),
                    EmployeeCategoryId = employeeCategoryId
                };

                var result = await _repository.GetDiseaseTrendDeptWiseAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportDeptWise(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            string? diseaseIds,
            int? employeeCategoryId)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseIds = ParseDiseaseIds(diseaseIds),
                    EmployeeCategoryId = employeeCategoryId
                };

                var fileBytes = await _repository.ExportDeptWiseToExcelAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                var fileName = $"DiseaseTrend_DeptWise_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting department wise report");
                TempData["Error"] = "Failed to export report: " + ex.Message;
                return RedirectToAction(nameof(DiseaseTrendDeptWise));
            }
        }

        #endregion

        #region Patient Wise Report

        [HttpGet]
        public async Task<IActionResult> DiseaseTrendPatientWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetPatientWiseData(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            string? diseaseIds,
            int? employeeCategoryId,
            string? fromPNo,
            string? toPNo,
            int page = 1,
            int pageSize = 100)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseIds = ParseDiseaseIds(diseaseIds),
                    EmployeeCategoryId = employeeCategoryId,
                    FromPNo = fromPNo,
                    ToPNo = toPNo
                };

                var result = await _repository.GetDiseaseTrendPatientWiseAsync(filter, userPlantId, currentUser, page, pageSize, isDoctor, userRole);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPatientWise(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            string? diseaseIds,
            int? employeeCategoryId,
            string? fromPNo,
            string? toPNo)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseIds = ParseDiseaseIds(diseaseIds),
                    EmployeeCategoryId = employeeCategoryId,
                    FromPNo = fromPNo,
                    ToPNo = toPNo
                };

                var fileBytes = await _repository.ExportPatientWiseToExcelAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                var fileName = $"DiseaseTrend_PatientWise_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting patient wise report");
                TempData["Error"] = "Failed to export report: " + ex.Message;
                return RedirectToAction(nameof(DiseaseTrendPatientWise));
            }
        }

        #endregion

        #region Medicine Wise Report

        [HttpGet]
        public async Task<IActionResult> DiseaseTrendMedicineWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetMedicineWiseData(
            DateTime? fromDate,
            DateTime? toDate)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var result = await _repository.GetDiseaseTrendMedicineWiseAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting medicine wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportMedicineWise(
            DateTime? fromDate,
            DateTime? toDate)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();
                var userRole = await GetUserRoleAsync();
                var isDoctor = IsDocterOrAdmin(userRole);

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var fileBytes = await _repository.ExportMedicineWiseToExcelAsync(filter, userPlantId, currentUser, isDoctor, userRole);
                var fileName = $"MedicinesConsumption_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting medicine wise report");
                TempData["Error"] = "Failed to export report: " + ex.Message;
                return RedirectToAction(nameof(DiseaseTrendMedicineWise));
            }
        }

        #endregion

        #region Dropdown Data Endpoints

        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _repository.GetDepartmentsAsync();
            return Json(departments);
        }

        [HttpGet]
        public async Task<IActionResult> GetDiseases()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            var diseases = await _repository.GetDiseasesAsync(userPlantId);
            return Json(diseases);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeTypes()
        {
            // ✅ FIX: Now returns Id (int) and Name from org_employee_category master table
            var employeeTypes = await _repository.GetEmployeeTypesAsync();
            return Json(employeeTypes);
        }

        #endregion
    }
}