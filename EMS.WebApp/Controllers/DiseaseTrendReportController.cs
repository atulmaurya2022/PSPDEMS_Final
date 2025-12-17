using EMS.WebApp.Models;
using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        /// <summary>
        /// Disease Trend Reports Landing Page
        /// </summary>
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
            if (string.IsNullOrEmpty(userName))
                return null;
            return await _repository.GetUserPlantIdAsync(userName);
        }

        private string GetCurrentUserName()
        {
            return User.Identity?.Name ?? "System";
        }

        #endregion

        #region Age Wise Report

        /// <summary>
        /// Disease Trend Analysis Age Wise Report View
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiseaseTrendAgeWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        /// <summary>
        /// Get Age Wise Report Data (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAgeWiseData(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            int? diseaseId,
            string? employeeType)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseId = diseaseId,
                    EmployeeType = employeeType
                };

                var result = await _repository.GetDiseaseTrendAgeWiseAsync(filter, userPlantId, currentUser);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting age wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Export Age Wise Report to Excel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportAgeWise(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            int? diseaseId,
            string? employeeType)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseId = diseaseId,
                    EmployeeType = employeeType
                };

                var fileBytes = await _repository.ExportAgeWiseToExcelAsync(filter, userPlantId, currentUser);
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

        /// <summary>
        /// Disease Trend Analysis Department Wise Report View
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiseaseTrendDeptWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        /// <summary>
        /// Get Department Wise Report Data (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDeptWiseData(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            int? diseaseId,
            string? employeeType)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseId = diseaseId,
                    EmployeeType = employeeType
                };

                var result = await _repository.GetDiseaseTrendDeptWiseAsync(filter, userPlantId, currentUser);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Export Department Wise Report to Excel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportDeptWise(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            int? diseaseId,
            string? employeeType)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseId = diseaseId,
                    EmployeeType = employeeType
                };

                var fileBytes = await _repository.ExportDeptWiseToExcelAsync(filter, userPlantId, currentUser);
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

        /// <summary>
        /// Disease Trend Analysis Patient Wise Report View
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiseaseTrendPatientWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        /// <summary>
        /// Get Patient Wise Report Data (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPatientWiseData(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            int? diseaseId,
            string? employeeType,
            string? fromPNo,
            string? toPNo,
            int page = 1,
            int pageSize = 100)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseId = diseaseId,
                    EmployeeType = employeeType,
                    FromPNo = fromPNo,
                    ToPNo = toPNo
                };

                var result = await _repository.GetDiseaseTrendPatientWiseAsync(filter, userPlantId, currentUser, page, pageSize);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Export Patient Wise Report to Excel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportPatientWise(
            DateTime? fromDate,
            DateTime? toDate,
            int? departmentId,
            int? diseaseId,
            string? employeeType,
            string? fromPNo,
            string? toPNo)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DepartmentId = departmentId,
                    DiseaseId = diseaseId,
                    EmployeeType = employeeType,
                    FromPNo = fromPNo,
                    ToPNo = toPNo
                };

                var fileBytes = await _repository.ExportPatientWiseToExcelAsync(filter, userPlantId, currentUser);
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

        /// <summary>
        /// Disease Trend Analysis Medicine Wise Report View (Medicines Consumption)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DiseaseTrendMedicineWise()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            ViewBag.FilterOptions = await _repository.GetFilterOptionsAsync(userPlantId);
            return View();
        }

        /// <summary>
        /// Get Medicine Wise Report Data (AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMedicineWiseData(
            DateTime? fromDate,
            DateTime? toDate)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var result = await _repository.GetDiseaseTrendMedicineWiseAsync(filter, userPlantId, currentUser);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting medicine wise data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Export Medicine Wise Report to Excel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportMedicineWise(
            DateTime? fromDate,
            DateTime? toDate)
        {
            try
            {
                var userPlantId = await GetCurrentUserPlantIdAsync();
                var currentUser = GetCurrentUserName();

                var filter = new DiseaseTrendFilterModel
                {
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var fileBytes = await _repository.ExportMedicineWiseToExcelAsync(filter, userPlantId, currentUser);
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

        /// <summary>
        /// Get Departments for dropdown
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _repository.GetDepartmentsAsync();
            return Json(departments);
        }

        /// <summary>
        /// Get Diseases for dropdown
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDiseases()
        {
            var userPlantId = await GetCurrentUserPlantIdAsync();
            var diseases = await _repository.GetDiseasesAsync(userPlantId);
            return Json(diseases);
        }

        /// <summary>
        /// Get Employee Types for dropdown
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetEmployeeTypes()
        {
            var employeeTypes = await _repository.GetEmployeeTypesAsync();
            return Json(employeeTypes);
        }

        #endregion
    }
}
