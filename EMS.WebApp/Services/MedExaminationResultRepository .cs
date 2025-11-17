using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using EMS.WebApp.Extensions;

namespace EMS.WebApp.Services
{
    public class MedExaminationResultRepository : IMedExaminationResultRepository
    {
        private readonly ApplicationDbContext _db;

        public MedExaminationResultRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<MedExaminationResultListItemViewModel>> GetResultsListAsync(int? userPlantId = null, string searchTerm = "", string? currentUsername = null)
        {
            try
            {
                var query = from result in _db.Set<MedExaminationResult>()
                            join employee in _db.HrEmployees on result.EmpUid equals employee.emp_uid
                            join category in _db.Set<MedExamCategory>() on result.CatId equals category.CatId
                            join location in _db.Set<Location>() on result.LocationId equals location.LocationId into locationGroup
                            from loc in locationGroup.DefaultIfEmpty()
                            join approval in _db.Set<MedExaminationApproval>() on result.ResultId equals approval.ResultId into approvalGroup
                            from appr in approvalGroup.DefaultIfEmpty()
                            select new
                            {
                                ResultId = result.ResultId,
                                EmpUid = result.EmpUid,
                                EmployeeId = employee.emp_id,
                                EmployeeName = employee.emp_name,
                                CategoryName = category.CatName,
                                CatId = result.CatId,
                                TestDate = result.TestDate,
                                TestLocationId = result.LocationId,
                                TestLocationName = loc != null ? loc.LocationName : "N/A",
                                Result = result.Result,
                                PlantId = result.PlantId,
                                CreatedBy = result.CreatedBy,
                                ApprovalStatus = appr != null ? appr.ApprovalStatus : "Pending"
                            };

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    query = query.Where(x => x.PlantId == plantIdAsShort);
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(x =>
                        x.EmployeeId.ToLower().Contains(searchTerm) ||
                        x.EmployeeName.ToLower().Contains(searchTerm) ||
                        x.CategoryName.ToLower().Contains(searchTerm) ||
                        (x.TestLocationName != null && x.TestLocationName.ToLower().Contains(searchTerm)) ||
                        (x.Result != null && x.Result.ToLower().Contains(searchTerm))
                    );
                }

                var results = await query
                    .OrderByDescending(x => x.TestDate)
                    .ToListAsync();

                return results.Select(r => new MedExaminationResultListItemViewModel
                {
                    ResultId = r.ResultId,
                    EmpUid = r.EmpUid,
                    EmployeeId = r.EmployeeId,
                    EmployeeName = r.EmployeeName,
                    CategoryName = r.CategoryName,
                    TestDate = r.TestDate?.ToDateTime(TimeOnly.MinValue),
                    TestDateFormatted = r.TestDate?.ToString("dd-MMM-yyyy") ?? "N/A",
                    TestLocationName = r.TestLocationName ?? "N/A",
                    Result = r.Result ?? "N/A",
                    CreatedBy = r.CreatedBy ?? "System",
                    ApprovalStatus = r.ApprovalStatus ?? "Pending",
                    CanEdit = !string.IsNullOrEmpty(currentUsername) && r.CreatedBy == currentUsername,
                    CanDelete = !string.IsNullOrEmpty(currentUsername) && r.CreatedBy == currentUsername
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetResultsListAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<MedExaminationResultViewModel?> GetResultForViewAsync(int resultId, int? userPlantId = null, string? currentUsername = null)
        {
            try
            {
                var resultQuery = _db.Set<MedExaminationResult>()
                    .Include(r => r.HrEmployee)
                        .ThenInclude(e => e.org_department)
                    .Include(r => r.HrEmployee)
                        .ThenInclude(e => e.org_employee_category)
                    .Include(r => r.HrEmployee)
                        .ThenInclude(e => e.org_plant)
                    .Include(r => r.MedExamCategory)
                    .Include(r => r.Location)
                    .Include(r => r.OrgPlant)
                    .Where(r => r.ResultId == resultId);

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    resultQuery = resultQuery.Where(r => r.PlantId == plantIdAsShort);
                }

                var result = await resultQuery.FirstOrDefaultAsync();

                if (result == null)
                {
                    return null;
                }

                // Get approval information
                var approval = await _db.Set<MedExaminationApproval>()
                    .FirstOrDefaultAsync(a => a.ResultId == resultId);

                var examCategories = await GetExamCategoriesAsync();
                var testLocations = await GetTestLocationsAsync();

                var viewModel = new MedExaminationResultViewModel
                {
                    ResultId = result.ResultId,
                    EmpNo = result.EmpUid,
                    CatId = result.CatId,
                    LastCheckupDate = result.LastCheckupDate?.ToDateTime(TimeOnly.MinValue),
                    TestDate = result.TestDate?.ToDateTime(TimeOnly.MinValue),
                    LocationId = result.LocationId,
                    Result = result.Result,
                    Remarks = result.Remarks,
                    PlantId = result.PlantId,
                    IsNewEntry = false,
                    EmployeeDetails = result.HrEmployee,
                    ExamCategories = examCategories,
                    TestLocations = testLocations,
                    OrgPlant = result.OrgPlant,
                    CreatedBy = result.CreatedBy,
                    ApprovalStatus = approval?.ApprovalStatus,
                    ApprovedBy = approval?.ApprovedBy,
                    ApprovedOn = approval?.ApprovedOn,
                    RejectionReason = approval?.RejectionReason,
                    CanEdit = !string.IsNullOrEmpty(currentUsername) && result.CreatedBy == currentUsername,
                    CanDelete = !string.IsNullOrEmpty(currentUsername) && result.CreatedBy == currentUsername
                };

                return viewModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetResultForViewAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<MedExaminationResultViewModel?> GetResultForEditAsync(int resultId, int? userPlantId = null, string? currentUsername = null)
        {
            try
            {
                var viewModel = await GetResultForViewAsync(resultId, userPlantId, currentUsername);

                if (viewModel == null)
                {
                    return null;
                }

                // Additional check: user can edit only if they are the creator and it's not approved
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    viewModel.CanEdit = viewModel.CreatedBy == currentUsername && viewModel.ApprovalStatus != "Approved";
                    viewModel.CanDelete = viewModel.CreatedBy == currentUsername && viewModel.ApprovalStatus != "Approved";
                }

                return viewModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetResultForEditAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CanUserEditResultAsync(int resultId, string username, int? userPlantId = null)
        {
            try
            {
                var resultQuery = _db.Set<MedExaminationResult>()
                    .Where(r => r.ResultId == resultId);

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    resultQuery = resultQuery.Where(r => r.PlantId == plantIdAsShort);
                }

                var result = await resultQuery.FirstOrDefaultAsync();

                if (result == null)
                {
                    return false;
                }

                // Check if user is creator
                if (result.CreatedBy != username)
                {
                    return false;
                }

                // Check approval status
                var approval = await _db.Set<MedExaminationApproval>()
                    .FirstOrDefaultAsync(a => a.ResultId == resultId);

                // Can edit only if not approved
                return approval?.ApprovalStatus != "Approved";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CanUserEditResultAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CanUserDeleteResultAsync(int resultId, string username, int? userPlantId = null)
        {
            try
            {
                // Same logic as edit for now
                return await CanUserEditResultAsync(resultId, username, userPlantId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CanUserDeleteResultAsync: {ex.Message}");
                return false;
            }
        }

        public async Task UpdateFormDataAsync(MedExaminationResultViewModel model, int? userPlantId = null, string? username = null)
        {
            try
            {
                if (!model.ResultId.HasValue)
                {
                    throw new ArgumentException("ResultId is required for update operation.");
                }

                var resultQuery = _db.Set<MedExaminationResult>()
                    .Where(r => r.ResultId == model.ResultId.Value);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    resultQuery = resultQuery.Where(r => r.PlantId == plantIdAsShort);
                }

                var examResult = await resultQuery.FirstOrDefaultAsync();

                if (examResult == null)
                {
                    throw new UnauthorizedAccessException("Access denied or record not found.");
                }

                // Check if user is creator
                if (!string.IsNullOrEmpty(username) && examResult.CreatedBy != username)
                {
                    throw new UnauthorizedAccessException("Only the creator can edit this result.");
                }

                // Check if approved
                var approval = await _db.Set<MedExaminationApproval>()
                    .FirstOrDefaultAsync(a => a.ResultId == model.ResultId.Value);

                if (approval?.ApprovalStatus == "Approved")
                {
                    throw new UnauthorizedAccessException("Cannot edit an approved result.");
                }

                // Update record with form data
                examResult.CatId = model.CatId ?? 0;
                examResult.LastCheckupDate = model.LastCheckupDate.HasValue ? DateOnly.FromDateTime(model.LastCheckupDate.Value) : null;
                examResult.TestDate = model.TestDate.HasValue ? DateOnly.FromDateTime(model.TestDate.Value) : null;
                examResult.LocationId = model.LocationId;
                examResult.Result = model.Result;
                examResult.Remarks = model.Remarks;
                examResult.ModifiedBy = username ?? "System";
                examResult.ModifiedOn = DateTime.Now;
                if (approval != null && approval.ApprovalStatus == "Rejected")
                {
                    approval.ApprovalStatus = "Pending";
                    approval.ApprovedBy = null; // Clear the rejector
                    approval.ApprovedOn = null; // Clear the rejection date
                    approval.RejectionReason = null; // Clear the rejection reason
                    approval.ModifiedBy = username ?? "System";
                    approval.ModifiedOn = DateTime.Now;
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateFormDataAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<MedExaminationResultViewModel> LoadFormData(int empNo, int? resultId = null, int? userPlantId = null)
        {
            try
            {
                var basicEmployee = await _db.HrEmployees
                    .Where(e => e.emp_uid == empNo)
                    .FirstOrDefaultAsync();

                if (basicEmployee == null)
                {
                    return null;
                }

                if (userPlantId.HasValue)
                {
                    if (basicEmployee.plant_id != userPlantId.Value)
                    {
                        return null;
                    }
                }

                OrgDepartment department = null;
                OrgEmployeeCategory employeeCategory = null;
                OrgPlant plant = null;

                try
                {
                    if (basicEmployee.dept_id > 0)
                    {
                        department = await _db.Set<OrgDepartment>()
                            .FirstOrDefaultAsync(d => d.dept_id == basicEmployee.dept_id);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoadFormData: Error loading department: {ex.Message}");
                }

                try
                {
                    if (basicEmployee.emp_category_id > 0)
                    {
                        employeeCategory = await _db.Set<OrgEmployeeCategory>()
                            .FirstOrDefaultAsync(c => c.emp_category_id == basicEmployee.emp_category_id);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoadFormData: Error loading category: {ex.Message}");
                }

                try
                {
                    if (basicEmployee.plant_id > 0)
                    {
                        plant = await _db.org_plants.FindAsync(basicEmployee.plant_id);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoadFormData: Error loading plant: {ex.Message}");
                }

                basicEmployee.org_department = department;
                basicEmployee.org_employee_category = employeeCategory;
                basicEmployee.org_plant = plant;

                List<MedExamCategory> examCategories;
                try
                {
                    examCategories = await _db.Set<MedExamCategory>()
                        .OrderBy(c => c.CatName)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    examCategories = new List<MedExamCategory>();
                }

                List<Location> testLocations;
                try
                {
                    testLocations = await GetTestLocationsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoadFormData: Error loading locations: {ex.Message}");
                    testLocations = new List<Location>();
                }

                DateTime? lastCheckupDate = null;
                try
                {
                    var previousExamQuery = _db.Set<MedExaminationResult>()
                        .Where(r => r.EmpUid == empNo);

                    if (userPlantId.HasValue)
                    {
                        short plantIdAsShort = (short)userPlantId.Value;
                        previousExamQuery = previousExamQuery.Where(r => r.PlantId == plantIdAsShort);
                    }

                    var lastExamination = await previousExamQuery
                        .OrderByDescending(r => r.TestDate)
                        .FirstOrDefaultAsync();

                    if (lastExamination != null && lastExamination.TestDate.HasValue)
                    {
                        lastCheckupDate = lastExamination.TestDate.Value.ToDateTime(TimeOnly.MinValue);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LoadFormData: Error loading previous examination: {ex.Message}");
                }

                var viewModel = new MedExaminationResultViewModel
                {
                    EmpNo = empNo,
                    IsNewEntry = true,
                    PlantId = (short?)userPlantId,
                    EmployeeDetails = basicEmployee,
                    ExamCategories = examCategories,
                    TestLocations = testLocations,
                    TestDate = DateTime.Now.Date,
                    LastCheckupDate = lastCheckupDate
                };

                return viewModel;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task SaveFormDataAsync(MedExaminationResultViewModel model, int? userPlantId = null, string? username = null)
        {
            MedExaminationResult examResult;

            if (model.IsNewEntry)
            {
                examResult = new MedExaminationResult
                {
                    EmpUid = model.EmpNo,
                    PlantId = (short?)userPlantId,
                    CreatedBy = username ?? "System",
                    CreatedOn = DateTime.Now
                };
                _db.Set<MedExaminationResult>().Add(examResult);
            }
            else
            {
                var resultQuery = _db.Set<MedExaminationResult>()
                    .Where(r => r.EmpUid == model.EmpNo);

                if (userPlantId.HasValue)
                {
                    resultQuery = resultQuery.Where(r => r.PlantId == userPlantId.Value);
                }

                examResult = await resultQuery.FirstOrDefaultAsync();

                if (examResult == null)
                {
                    throw new UnauthorizedAccessException("Access denied or record not found.");
                }

                examResult.ModifiedBy = username ?? "System";
                examResult.ModifiedOn = DateTime.Now;
            }

            examResult.CatId = model.CatId ?? 0;
            examResult.LastCheckupDate = model.LastCheckupDate.HasValue ? DateOnly.FromDateTime(model.LastCheckupDate.Value) : null;
            examResult.TestDate = model.TestDate.HasValue ? DateOnly.FromDateTime(model.TestDate.Value) : null;
            examResult.LocationId = model.LocationId;
            examResult.Result = model.Result;
            examResult.Remarks = model.Remarks;

            await _db.SaveChangesAsync();

            try
            {
                if (model.IsNewEntry && examResult.ResultId > 0)
                {
                    var approval = new MedExaminationApproval
                    {
                        ResultId = examResult.ResultId,
                        EmpUid = model.EmpNo,
                        CatId = model.CatId ?? 1,
                        ApprovalStatus = "Pending",
                        PlantId = (short?)userPlantId,
                        CreatedBy = username ?? "System",
                        CreatedOn = DateTime.Now
                    };
                    _db.Set<MedExaminationApproval>().Add(approval);
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating approval record: {ex.Message}");
            }
        }

        public async Task<List<MedExaminationResult>> GetEmployeeExamResultsAsync(int empNo, int? userPlantId = null)
        {
            var resultQuery = _db.Set<MedExaminationResult>()
                .Include(r => r.MedExamCategory)
                .Include(r => r.Location)
                .Where(r => r.EmpUid == empNo);

            if (userPlantId.HasValue)
            {
                resultQuery = resultQuery.Where(r => r.PlantId == userPlantId.Value);
            }

            return await resultQuery
                .OrderByDescending(r => r.TestDate)
                .ToListAsync();
        }

        public async Task<List<MedExamCategory>> GetExamCategoriesAsync()
        {
            return await _db.Set<MedExamCategory>()
                .Include(c => c.med_criteria)
                .OrderBy(c => c.CatName)
                .ToListAsync();
        }

        public async Task<List<Location>> GetTestLocationsAsync()
        {
            return await _db.Set<Location>()
                .OrderBy(l => l.LocationName)
                .ToListAsync();
        }

        public async Task<List<string>> GetMatchingEmployeeIdsAsync(string term, int? userPlantId = null)
        {
            var employeeQuery = _db.HrEmployees
                .Where(e => e.emp_id.StartsWith(term));

            if (userPlantId.HasValue)
            {
                employeeQuery = employeeQuery.Where(e => e.plant_id == userPlantId.Value);
            }

            return await employeeQuery
                .OrderBy(e => e.emp_id)
                .Select(e => e.emp_id)
                .Take(10)
                .ToListAsync();
        }

        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<bool> IsUserAuthorizedForEmployeeAsync(int empNo, int userPlantId)
        {
            return await _db.HrEmployees.AnyAsync(e => e.emp_uid == empNo && e.plant_id == userPlantId);
        }

        public async Task<bool> DeleteExamResultAsync(int resultId, int? userPlantId = null)
        {
            var resultQuery = _db.Set<MedExaminationResult>()
                .Where(r => r.ResultId == resultId);

            if (userPlantId.HasValue)
            {
                resultQuery = resultQuery.Where(r => r.PlantId == userPlantId.Value);
            }

            var examResult = await resultQuery.FirstOrDefaultAsync();

            if (examResult == null)
            {
                return false;
            }

            // Delete related approval records first
            var approvals = await _db.Set<MedExaminationApproval>()
                .Where(a => a.ResultId == resultId)
                .ToListAsync();

            if (approvals.Any())
            {
                _db.Set<MedExaminationApproval>().RemoveRange(approvals);
            }

            _db.Set<MedExaminationResult>().Remove(examResult);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<MedExaminationResult?> GetExamResultByIdAsync(int resultId, int? userPlantId = null)
        {
            var resultQuery = _db.Set<MedExaminationResult>()
                .Include(r => r.HrEmployee)
                .Include(r => r.MedExamCategory)
                .Include(r => r.Location)
                .Include(r => r.OrgPlant)
                .Where(r => r.ResultId == resultId);

            if (userPlantId.HasValue)
            {
                resultQuery = resultQuery.Where(r => r.PlantId == userPlantId.Value);
            }

            return await resultQuery.FirstOrDefaultAsync();
        }
    }
}