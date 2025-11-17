using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class MedExaminationApprovalRepository : IMedExaminationApprovalRepository
    {
        private readonly ApplicationDbContext _db;

        public MedExaminationApprovalRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<MedExaminationApprovalViewModel> GetApprovalDataAsync(
    int? categoryId = null,
    int? locationId = null,
    string? approvalStatus = null, // Changed default to null to show all
    int? userPlantId = null)
        {
            try
            {
                // Start with a query that gets examination results with their related data
                var query = from result in _db.Set<MedExaminationResult>()
                            join employee in _db.HrEmployees on result.EmpUid equals employee.emp_uid
                            join category in _db.Set<MedExamCategory>() on result.CatId equals category.CatId
                            join location in _db.Set<Location>() on result.LocationId equals location.LocationId into locationGroup
                            from loc in locationGroup.DefaultIfEmpty()
                            join department in _db.Set<OrgDepartment>() on employee.dept_id equals department.dept_id into deptGroup
                            from dept in deptGroup.DefaultIfEmpty()
                            join approval in _db.Set<MedExaminationApproval>() on result.ResultId equals approval.ResultId into approvalGroup
                            from appr in approvalGroup.DefaultIfEmpty()
                            select new
                            {
                                ResultId = result.ResultId,
                                EmpUid = result.EmpUid,
                                EmployeeId = employee.emp_id,
                                EmployeeName = employee.emp_name,
                                EmployeeDOB = employee.emp_DOB,
                                LocationId = result.LocationId,
                                TestLocation = loc != null ? loc.LocationName : "",
                                TestDate = result.TestDate,
                                DepartmentName = dept != null ? dept.dept_name : "",
                                CategoryName = category.CatName,
                                CatId = result.CatId,
                                PlantId = result.PlantId,
                                ApprovalId = appr != null ? appr.ApprovalId : 0,
                                ApprovalStatus = appr != null ? appr.ApprovalStatus : "Pending",
                                ApprovedOn = appr != null ? appr.ApprovedOn : null,
                                ApprovedBy = appr != null ? appr.ApprovedBy : null,
                                RejectionReason = appr != null ? appr.RejectionReason : null
                            };

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    query = query.Where(x => x.PlantId == plantIdAsShort);
                }

                // Apply filters
                if (categoryId.HasValue)
                {
                    query = query.Where(x => x.CatId == categoryId.Value);
                }

                if (locationId.HasValue)
                {
                    query = query.Where(x => x.LocationId == locationId.Value);
                }

                // Only filter by status if a specific status is provided
                // If approvalStatus is null or empty, show all records
                if (!string.IsNullOrEmpty(approvalStatus))
                {
                    query = query.Where(x => x.ApprovalStatus == approvalStatus);
                }

                var results = await query
                    .OrderByDescending(x => x.TestDate) // Show most recent tests first
                    .ToListAsync();

                // Transform to view model items
                var approvalItems = results.Select(r => new MedExaminationApprovalItemViewModel
                {
                    ApprovalId = r.ApprovalId,
                    ResultId = r.ResultId,
                    EmpUid = r.EmpUid,
                    EmployeeNo = r.EmployeeId,
                    EmployeeName = r.EmployeeName,
                    LocationId = r.LocationId,
                    TestLocation = r.TestLocation,
                    LastTestDate = r.TestDate?.ToDateTime(TimeOnly.MinValue),
                    Department = r.DepartmentName,
                    CategoryName = r.CategoryName,
                    Age = CalculateAge(r.EmployeeDOB),
                    ApprovalStatus = r.ApprovalStatus ?? "Pending",
                    ApprovedOn = r.ApprovedOn,
                    ApprovedBy = r.ApprovedBy,
                    RejectionReason = r.RejectionReason
                }).ToList();

                // Get reference data
                var examCategories = await GetExamCategoriesAsync();
                var testLocations = await GetTestLocationsAsync();

                // Create view model
                var viewModel = new MedExaminationApprovalViewModel
                {
                    SelectedCategoryId = categoryId,
                    SelectedLocationId = locationId,
                    SelectedApprovalStatus = approvalStatus,
                    PlantId = (short?)userPlantId,
                    ExamCategories = examCategories,
                    TestLocations = testLocations,
                    ApprovalItems = approvalItems
                };

                return viewModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetApprovalDataAsync: {ex.Message}");
                throw;
            }
        }
        public async Task<int> ApproveExaminationsAsync(List<int> approvalIds, string approvedBy, int? userPlantId = null)
        {
            try
            {
                var approvals = await _db.Set<MedExaminationApproval>()
                    .Where(a => approvalIds.Contains(a.ApprovalId))
                    .ToListAsync();

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    approvals = approvals.Where(a => a.PlantId == plantIdAsShort).ToList();
                }

                foreach (var approval in approvals)
                {
                    approval.ApprovalStatus = "Approved";
                    approval.ApprovedBy = approvedBy;
                    approval.ApprovedOn = DateTime.Now;
                    approval.ModifiedBy = approvedBy;
                    approval.ModifiedOn = DateTime.Now;
                }

                await _db.SaveChangesAsync();
                return approvals.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ApproveExaminationsAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<int> RejectExaminationsAsync(List<int> approvalIds, string rejectedBy, string? rejectionReason = null, int? userPlantId = null)
        {
            try
            {
                var approvals = await _db.Set<MedExaminationApproval>()
                    .Where(a => approvalIds.Contains(a.ApprovalId))
                    .ToListAsync();

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    approvals = approvals.Where(a => a.PlantId == plantIdAsShort).ToList();
                }

                foreach (var approval in approvals)
                {
                    approval.ApprovalStatus = "Rejected";
                    approval.ApprovedBy = rejectedBy;
                    approval.ApprovedOn = DateTime.Now;
                    approval.RejectionReason = rejectionReason;
                    approval.ModifiedBy = rejectedBy;
                    approval.ModifiedOn = DateTime.Now;
                }

                await _db.SaveChangesAsync();
                return approvals.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RejectExaminationsAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<int> UnApproveExaminationsAsync(List<int> approvalIds, string unApprovedBy, int? userPlantId = null)
        {
            try
            {
                var approvals = await _db.Set<MedExaminationApproval>()
                    .Where(a => approvalIds.Contains(a.ApprovalId))
                    .ToListAsync();

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    approvals = approvals.Where(a => a.PlantId == plantIdAsShort).ToList();
                }

                // Only process records that are currently "Approved"
                var approvedRecords = approvals.Where(a => a.ApprovalStatus == "Approved").ToList();

                foreach (var approval in approvedRecords)
                {
                    // Change status back to "Pending" instead of "UnApproved"
                    approval.ApprovalStatus = "Pending";
                    approval.ApprovedBy = null; // Clear the approver
                    approval.ApprovedOn = null; // Clear the approval date
                    approval.RejectionReason = null; // Clear any rejection reason
                    approval.ModifiedBy = unApprovedBy;
                    approval.ModifiedOn = DateTime.Now;
                }

                await _db.SaveChangesAsync();
                return approvedRecords.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UnApproveExaminationsAsync: {ex.Message}");
                throw;
            }
        }
        public async Task<List<MedExamCategory>> GetExamCategoriesAsync()
        {
            try
            {
                return await _db.Set<MedExamCategory>()
                    .OrderBy(c => c.CatName)
                    .ToListAsync();
            }
            catch
            {
                return new List<MedExamCategory>();
            }
        }

        public async Task<List<Location>> GetTestLocationsAsync()
        {
            try
            {
                return await _db.Set<Location>()
                    .OrderBy(l => l.LocationName)
                    .ToListAsync();
            }
            catch
            {
                return new List<Location>();
            }
        }

        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<bool> CreateApprovalRecordAsync(int resultId, int? userPlantId = null)
        {
            try
            {
                // Check if approval record already exists
                var existingApproval = await _db.Set<MedExaminationApproval>()
                    .FirstOrDefaultAsync(a => a.ResultId == resultId);

                if (existingApproval != null)
                {
                    return true; // Already exists
                }

                // Get the examination result
                var result = await _db.Set<MedExaminationResult>()
                    .FirstOrDefaultAsync(r => r.ResultId == resultId);

                if (result == null)
                {
                    return false; // Result not found
                }

                // Create new approval record
                var approval = new MedExaminationApproval
                {
                    ResultId = resultId,
                    EmpUid = result.EmpUid,
                    CatId = result.CatId,
                    ApprovalStatus = "Pending",
                    PlantId = (short?)userPlantId,
                    CreatedBy = "System",
                    CreatedOn = DateTime.Now
                };

                _db.Set<MedExaminationApproval>().Add(approval);
                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateApprovalRecordAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<ApprovalStatistics> GetApprovalStatisticsAsync(int? userPlantId = null)
        {
            try
            {
                var query = _db.Set<MedExaminationApproval>().AsQueryable();

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    short plantIdAsShort = (short)userPlantId.Value;
                    query = query.Where(a => a.PlantId == plantIdAsShort);
                }

                var stats = await query
                    .GroupBy(a => a.ApprovalStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                return new ApprovalStatistics
                {
                    TotalPending = stats.FirstOrDefault(s => s.Status == "Pending")?.Count ?? 0,
                    TotalApproved = stats.FirstOrDefault(s => s.Status == "Approved")?.Count ?? 0,
                    TotalRejected = stats.FirstOrDefault(s => s.Status == "Rejected")?.Count ?? 0,
                    TotalUnApproved = stats.FirstOrDefault(s => s.Status == "UnApproved")?.Count ?? 0
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetApprovalStatisticsAsync: {ex.Message}");
                return new ApprovalStatistics();
            }
        }

        private int? CalculateAge(DateOnly? birthDate)
        {
            if (!birthDate.HasValue)
                return null;

            var today = DateOnly.FromDateTime(DateTime.Today);
            int age = today.Year - birthDate.Value.Year;
            if (today < birthDate.Value.AddYears(age))
                age--;
            return age;
        }
    }
}