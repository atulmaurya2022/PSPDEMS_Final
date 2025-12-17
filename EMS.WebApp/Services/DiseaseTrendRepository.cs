using EMS.WebApp.Data;
using EMS.WebApp.Models;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace EMS.WebApp.Services
{
    public class DiseaseTrendRepository : IDiseaseTrendRepository
    {
        private readonly ApplicationDbContext _db;

        public DiseaseTrendRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        #region Common Methods

        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            try
            {
                var user = await _db.SysUsers
                    .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);
                return user?.plant_id; // short will be implicitly converted to int?
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting plant ID for user {userName}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetPlantCodeByIdAsync(int plantId)
        {
            try
            {
                var plant = await _db.org_plants.FirstOrDefaultAsync(p => p.plant_id == plantId);
                return plant?.plant_code;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting plant code for plant ID {plantId}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetPlantNameByIdAsync(int plantId)
        {
            try
            {
                var plant = await _db.org_plants.FirstOrDefaultAsync(p => p.plant_id == plantId);
                return plant?.plant_name;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting plant name for plant ID {plantId}: {ex.Message}");
                return null;
            }
        }

        public async Task<DiseaseTrendFilterOptions> GetFilterOptionsAsync(int? userPlantId = null)
        {
            return new DiseaseTrendFilterOptions
            {
                Departments = await GetDepartmentsAsync(),
                Diseases = await GetDiseasesAsync(userPlantId),
                Plants = await GetPlantsAsync(),
                EmployeeTypes = await GetEmployeeTypesAsync()
            };
        }

        public async Task<List<DropdownItem>> GetDepartmentsAsync()
        {
            return await _db.org_departments
                .OrderBy(d => d.dept_name)
                .Select(d => new DropdownItem
                {
                    Id = d.dept_id,
                    Name = d.dept_name
                })
                .ToListAsync();
        }

        public async Task<List<DropdownItem>> GetDiseasesAsync(int? userPlantId = null)
        {
            var query = _db.MedDiseases.AsQueryable();

            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.plant_id == userPlantId.Value);
            }

            return await query
                .OrderBy(d => d.DiseaseName)
                .Select(d => new DropdownItem
                {
                    Id = d.DiseaseId,
                    Name = d.DiseaseName
                })
                .ToListAsync();
        }

        public async Task<List<DropdownItem>> GetPlantsAsync()
        {
            return await _db.org_plants
                .OrderBy(p => p.plant_name)
                .Select(p => new DropdownItem
                {
                    Id = p.plant_id,
                    Name = p.plant_name
                })
                .ToListAsync();
        }

        public async Task<List<DropdownItem>> GetEmployeeTypesAsync()
        {
            // Return static employee types
            return await Task.FromResult(new List<DropdownItem>
            {
                new() { Id = 1, Name = "TR EMPLOYEE" },
                new() { Id = 2, Name = "CONTRACT" },
                new() { Id = 3, Name = "TRAINEE" },
                new() { Id = 4, Name = "DEPENDENT" },
                new() { Id = 5, Name = "OTHERS" }
            });
        }

        private async Task<ReportHeaderInfo> GetReportHeaderAsync(int? userPlantId, string? currentUser)
        {
            var header = new ReportHeaderInfo
            {
                GeneratedOn = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt"),
                GeneratedBy = currentUser ?? "System"
            };

            if (userPlantId.HasValue)
            {
                header.PlantCode = await GetPlantCodeByIdAsync(userPlantId.Value) ?? "N/A";
                header.PlantName = await GetPlantNameByIdAsync(userPlantId.Value) ?? "Unknown Plant";
            }

            return header;
        }

        #endregion

        #region Age Wise Report

        public async Task<DiseaseTrendAgeWiseReportResponse> GetDiseaseTrendAgeWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null)
        {
            var response = new DiseaseTrendAgeWiseReportResponse
            {
                ReportInfo = await GetReportHeaderAsync(userPlantId, currentUser)
            };

            response.ReportInfo.FromDate = filter.FromDate;
            response.ReportInfo.ToDate = filter.ToDate;

            try
            {
                // Query employee prescriptions with diseases
                var query = from p in _db.MedPrescriptions
                            join pd in _db.MedPrescriptionDiseases on p.PrescriptionId equals pd.PrescriptionId
                            join d in _db.MedDiseases on pd.DiseaseId equals d.DiseaseId
                            join e in _db.HrEmployees on p.emp_uid equals e.emp_uid
                            where p.ApprovalStatus == "Approved"
                            select new
                            {
                                p.PrescriptionId,
                                p.PrescriptionDate,
                                p.PlantId,
                                d.DiseaseId,
                                d.DiseaseName,
                                e.emp_uid,
                                e.emp_id,
                                e.emp_DOB,
                                e.dept_id,
                                e.emp_category_id
                            };

                // Apply filters
                if (userPlantId.HasValue)
                    query = query.Where(x => x.PlantId == userPlantId.Value);

                if (filter.FromDate.HasValue)
                    query = query.Where(x => x.PrescriptionDate >= filter.FromDate.Value);

                if (filter.ToDate.HasValue)
                    query = query.Where(x => x.PrescriptionDate <= filter.ToDate.Value.AddDays(1));

                if (filter.DepartmentId.HasValue)
                    query = query.Where(x => x.dept_id == filter.DepartmentId.Value);

                if (filter.DiseaseId.HasValue)
                    query = query.Where(x => x.DiseaseId == filter.DiseaseId.Value);

                var results = await query.ToListAsync();

                // Calculate ages and group
                var today = DateOnly.FromDateTime(DateTime.Today);
                var groupedData = results
                    .Select(r => new
                    {
                        r.DiseaseName,
                        r.emp_uid,
                        Age = r.emp_DOB.HasValue ? today.Year - r.emp_DOB.Value.Year -
                            (r.emp_DOB.Value > today.AddYears(-(today.Year - r.emp_DOB.Value.Year)) ? 1 : 0) : (int?)null
                    })
                    .Where(r => r.Age.HasValue)
                    .GroupBy(r => new
                    {
                        r.DiseaseName,
                        AgeGroup = GetAgeGroup(r.Age.Value)
                    })
                    .Select(g => new DiseaseTrendAgeWiseViewModel
                    {
                        DiseaseName = g.Key.DiseaseName,
                        AgeGroup = g.Key.AgeGroup,
                        EmployeeCount = g.Select(x => x.emp_uid).Distinct().Count()
                    })
                    .OrderBy(x => x.DiseaseName)
                    .ThenBy(x => x.AgeGroup)
                    .ToList();

                // Add serial numbers
                int slNo = 1;
                foreach (var item in groupedData)
                {
                    item.SlNo = slNo++;
                }

                response.Data = groupedData;
                response.Summary = new DiseaseTrendAgeWiseSummary
                {
                    TotalRecords = groupedData.Count,
                    TotalEmployees = groupedData.Sum(x => x.EmployeeCount),
                    TotalDiseases = groupedData.Select(x => x.DiseaseName).Distinct().Count(),
                    TotalAgeGroups = groupedData.Select(x => x.AgeGroup).Distinct().Count()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetDiseaseTrendAgeWiseAsync: {ex.Message}");
            }

            return response;
        }

        private string GetAgeGroup(int age)
        {
            return age switch
            {
                < 20 => "Below 20",
                >= 20 and < 30 => "20-29",
                >= 30 and < 40 => "30-39",
                >= 40 and < 50 => "40-49",
                >= 50 and < 60 => "50-59",
                _ => "60 & Above"
            };
        }

        #endregion

        #region Department Wise Report

        public async Task<DiseaseTrendDeptWiseReportResponse> GetDiseaseTrendDeptWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null)
        {
            var response = new DiseaseTrendDeptWiseReportResponse
            {
                ReportInfo = await GetReportHeaderAsync(userPlantId, currentUser)
            };

            response.ReportInfo.FromDate = filter.FromDate;
            response.ReportInfo.ToDate = filter.ToDate;

            try
            {
                // Query employee prescriptions with diseases and departments
                var query = from p in _db.MedPrescriptions
                            join pd in _db.MedPrescriptionDiseases on p.PrescriptionId equals pd.PrescriptionId
                            join d in _db.MedDiseases on pd.DiseaseId equals d.DiseaseId
                            join e in _db.HrEmployees on p.emp_uid equals e.emp_uid
                            join dept in _db.org_departments on e.dept_id equals dept.dept_id
                            where p.ApprovalStatus == "Approved"
                            select new
                            {
                                p.PrescriptionId,
                                p.PrescriptionDate,
                                p.PlantId,
                                d.DiseaseId,
                                d.DiseaseName,
                                e.emp_uid,
                                dept.dept_id,
                                dept.dept_name,
                                e.emp_category_id
                            };

                // Apply filters
                if (userPlantId.HasValue)
                    query = query.Where(x => x.PlantId == userPlantId.Value);

                if (filter.FromDate.HasValue)
                    query = query.Where(x => x.PrescriptionDate >= filter.FromDate.Value);

                if (filter.ToDate.HasValue)
                    query = query.Where(x => x.PrescriptionDate <= filter.ToDate.Value.AddDays(1));

                if (filter.DepartmentId.HasValue)
                    query = query.Where(x => x.dept_id == filter.DepartmentId.Value);

                if (filter.DiseaseId.HasValue)
                    query = query.Where(x => x.DiseaseId == filter.DiseaseId.Value);

                var results = await query.ToListAsync();

                // Group by disease and department
                var groupedData = results
                    .GroupBy(r => new { r.DiseaseName, r.DiseaseId, r.dept_id, r.dept_name })
                    .Select(g => new DiseaseTrendDeptWiseViewModel
                    {
                        DiseaseName = g.Key.DiseaseName,
                        DiseaseId = g.Key.DiseaseId,
                        DepartmentId = g.Key.dept_id,
                        DepartmentName = g.Key.dept_name,
                        EmployeeCount = g.Select(x => x.emp_uid).Distinct().Count()
                    })
                    .OrderBy(x => x.DiseaseName)
                    .ThenBy(x => x.DepartmentName)
                    .ToList();

                // Add serial numbers
                int slNo = 1;
                foreach (var item in groupedData)
                {
                    item.SlNo = slNo++;
                }

                response.Data = groupedData;
                response.Summary = new DiseaseTrendDeptWiseSummary
                {
                    TotalRecords = groupedData.Count,
                    TotalEmployees = groupedData.Sum(x => x.EmployeeCount),
                    TotalDiseases = groupedData.Select(x => x.DiseaseName).Distinct().Count(),
                    TotalDepartments = groupedData.Select(x => x.DepartmentName).Distinct().Count()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetDiseaseTrendDeptWiseAsync: {ex.Message}");
            }

            return response;
        }

        #endregion

        #region Patient Wise Report

        public async Task<DiseaseTrendPatientWiseReportResponse> GetDiseaseTrendPatientWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null,
            int page = 1,
            int pageSize = 100)
        {
            var response = new DiseaseTrendPatientWiseReportResponse
            {
                ReportInfo = await GetReportHeaderAsync(userPlantId, currentUser)
            };

            response.ReportInfo.FromDate = filter.FromDate;
            response.ReportInfo.ToDate = filter.ToDate;

            try
            {
                // Combined query for employees and other patients
                var employeeQuery = from p in _db.MedPrescriptions
                                    join pd in _db.MedPrescriptionDiseases on p.PrescriptionId equals pd.PrescriptionId
                                    join d in _db.MedDiseases on pd.DiseaseId equals d.DiseaseId
                                    join pm in _db.MedPrescriptionMedicines on p.PrescriptionId equals pm.PrescriptionId into medicines
                                    from med in medicines.DefaultIfEmpty()
                                    join m in _db.med_masters on med.MedItemId equals m.MedItemId into meds
                                    from medicine in meds.DefaultIfEmpty()
                                    join e in _db.HrEmployees on p.emp_uid equals e.emp_uid
                                    join dept in _db.org_departments on e.dept_id equals dept.dept_id into depts
                                    from department in depts.DefaultIfEmpty()
                                    where p.ApprovalStatus == "Approved"
                                    select new
                                    {
                                        EmpNo = e.emp_id,
                                        PatientName = e.emp_name,
                                        DiseaseName = d.DiseaseName,
                                        MedicineName = medicine != null ? medicine.MedItemName : "",
                                        DateTimeVisit = p.PrescriptionDate,
                                        DepartmentName = department != null ? department.dept_name : "",
                                        PatientType = "Employee",
                                        Age = e.emp_DOB.HasValue ?
                                            DateTime.Today.Year - e.emp_DOB.Value.Year -
                                            (e.emp_DOB.Value > DateOnly.FromDateTime(DateTime.Today.AddYears(-(DateTime.Today.Year - e.emp_DOB.Value.Year))) ? 1 : 0)
                                            : (int?)null,
                                        PlantId = p.PlantId,
                                        p.PrescriptionDate,
                                        DiseaseId = d.DiseaseId,
                                        DeptId = e.dept_id
                                    };

                // Apply filters to employee query
                if (userPlantId.HasValue)
                    employeeQuery = employeeQuery.Where(x => x.PlantId == userPlantId.Value);

                if (filter.FromDate.HasValue)
                    employeeQuery = employeeQuery.Where(x => x.PrescriptionDate >= filter.FromDate.Value);

                if (filter.ToDate.HasValue)
                    employeeQuery = employeeQuery.Where(x => x.PrescriptionDate <= filter.ToDate.Value.AddDays(1));

                if (filter.DepartmentId.HasValue)
                    employeeQuery = employeeQuery.Where(x => x.DeptId == filter.DepartmentId.Value);

                if (filter.DiseaseId.HasValue)
                    employeeQuery = employeeQuery.Where(x => x.DiseaseId == filter.DiseaseId.Value);

                if (!string.IsNullOrEmpty(filter.FromPNo))
                    employeeQuery = employeeQuery.Where(x => string.Compare(x.EmpNo, filter.FromPNo) >= 0);

                if (!string.IsNullOrEmpty(filter.ToPNo))
                    employeeQuery = employeeQuery.Where(x => string.Compare(x.EmpNo, filter.ToPNo) <= 0);

                // Query for other patients (non-employees)
                var othersQuery = from od in _db.OthersDiagnoses
                                  join op in _db.OtherPatients on od.PatientId equals op.PatientId
                                  join odd in _db.OthersDiagnosisDiseases on od.DiagnosisId equals odd.DiagnosisId
                                  join d in _db.MedDiseases on odd.DiseaseId equals d.DiseaseId
                                  join odm in _db.OthersDiagnosisMedicines on od.DiagnosisId equals odm.DiagnosisId into medicines
                                  from med in medicines.DefaultIfEmpty()
                                  join m in _db.med_masters on med.MedItemId equals m.MedItemId into meds
                                  from medicine in meds.DefaultIfEmpty()
                                  where od.ApprovalStatus == "Approved"
                                  select new
                                  {
                                      EmpNo = op.TreatmentId,
                                      PatientName = op.PatientName,
                                      DiseaseName = d.DiseaseName,
                                      MedicineName = medicine != null ? medicine.MedItemName : "",
                                      DateTimeVisit = od.VisitDate,
                                      DepartmentName = "",
                                      PatientType = "Others",
                                      Age = op.Age,
                                      PlantId = od.PlantId,
                                      PrescriptionDate = od.VisitDate,
                                      DiseaseId = d.DiseaseId,
                                      DeptId = (short)0
                                  };

                // Apply filters to others query
                if (userPlantId.HasValue)
                    othersQuery = othersQuery.Where(x => x.PlantId == userPlantId.Value);

                if (filter.FromDate.HasValue)
                    othersQuery = othersQuery.Where(x => x.PrescriptionDate >= filter.FromDate.Value);

                if (filter.ToDate.HasValue)
                    othersQuery = othersQuery.Where(x => x.PrescriptionDate <= filter.ToDate.Value.AddDays(1));

                if (filter.DiseaseId.HasValue)
                    othersQuery = othersQuery.Where(x => x.DiseaseId == filter.DiseaseId.Value);

                // Execute queries
                var employeeResults = await employeeQuery.ToListAsync();
                var othersResults = await othersQuery.ToListAsync();

                // Combine and process results
                var combinedResults = employeeResults
                    .Select(r => new DiseaseTrendPatientWiseViewModel
                    {
                        EmpNo = r.EmpNo,
                        PatientName = r.PatientName,
                        DiseaseName = r.DiseaseName,
                        MedicineName = r.MedicineName ?? "",
                        DateTimeVisit = r.DateTimeVisit,
                        DepartmentName = r.DepartmentName ?? "",
                        PatientType = r.PatientType,
                        Age = r.Age
                    })
                    .Concat(othersResults.Select(r => new DiseaseTrendPatientWiseViewModel
                    {
                        EmpNo = r.EmpNo,
                        PatientName = r.PatientName,
                        DiseaseName = r.DiseaseName,
                        MedicineName = r.MedicineName ?? "",
                        DateTimeVisit = r.DateTimeVisit,
                        DepartmentName = r.DepartmentName ?? "",
                        PatientType = r.PatientType,
                        Age = r.Age
                    }))
                    .OrderByDescending(x => x.DateTimeVisit)
                    .ToList();

                // Pagination
                var totalRecords = combinedResults.Count;
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                var paginatedData = combinedResults
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Add serial numbers
                int slNo = (page - 1) * pageSize + 1;
                foreach (var item in paginatedData)
                {
                    item.SlNo = slNo++;
                }

                response.Data = paginatedData;
                response.TotalPages = totalPages;
                response.CurrentPage = page;
                response.Summary = new DiseaseTrendPatientWiseSummary
                {
                    TotalRecords = totalRecords,
                    TotalPatients = combinedResults.Select(x => x.EmpNo).Distinct().Count(),
                    TotalDiseases = combinedResults.Select(x => x.DiseaseName).Distinct().Count(),
                    TotalMedicines = combinedResults.Where(x => !string.IsNullOrEmpty(x.MedicineName)).Select(x => x.MedicineName).Distinct().Count(),
                    TotalVisits = totalRecords
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetDiseaseTrendPatientWiseAsync: {ex.Message}");
            }

            return response;
        }

        #endregion

        #region Medicine Wise Report

        public async Task<DiseaseTrendMedicineWiseReportResponse> GetDiseaseTrendMedicineWiseAsync(
            DiseaseTrendFilterModel filter,
            int? userPlantId = null,
            string? currentUser = null)
        {
            var response = new DiseaseTrendMedicineWiseReportResponse
            {
                ReportInfo = await GetReportHeaderAsync(userPlantId, currentUser)
            };

            response.ReportInfo.FromDate = filter.FromDate;
            response.ReportInfo.ToDate = filter.ToDate;

            try
            {
                // Query for employee prescriptions
                var employeeQuery = from pm in _db.MedPrescriptionMedicines
                                    join p in _db.MedPrescriptions on pm.PrescriptionId equals p.PrescriptionId
                                    join m in _db.med_masters on pm.MedItemId equals m.MedItemId
                                    join mb in _db.med_bases on m.BaseId equals mb.BaseId into bases
                                    from baseName in bases.DefaultIfEmpty()
                                    where p.ApprovalStatus == "Approved"
                                    select new
                                    {
                                        pm.MedItemId,
                                        m.MedItemName,
                                        m.CompanyName,
                                        BaseName = baseName != null ? baseName.BaseName : "",
                                        pm.Quantity,
                                        p.CreatedBy,
                                        p.PrescriptionDate,
                                        p.PlantId
                                    };

                // Query for others diagnoses
                var othersQuery = from odm in _db.OthersDiagnosisMedicines
                                  join od in _db.OthersDiagnoses on odm.DiagnosisId equals od.DiagnosisId
                                  join m in _db.med_masters on odm.MedItemId equals m.MedItemId
                                  join mb in _db.med_bases on m.BaseId equals mb.BaseId into bases
                                  from baseName in bases.DefaultIfEmpty()
                                  where od.ApprovalStatus == "Approved"
                                  select new
                                  {
                                      odm.MedItemId,
                                      m.MedItemName,
                                      m.CompanyName,
                                      BaseName = baseName != null ? baseName.BaseName : "",
                                      odm.Quantity,
                                      od.CreatedBy,
                                      PrescriptionDate = od.VisitDate,
                                      od.PlantId
                                  };

                // Apply filters
                if (userPlantId.HasValue)
                {
                    employeeQuery = employeeQuery.Where(x => x.PlantId == userPlantId.Value);
                    othersQuery = othersQuery.Where(x => x.PlantId == userPlantId.Value);
                }

                if (filter.FromDate.HasValue)
                {
                    employeeQuery = employeeQuery.Where(x => x.PrescriptionDate >= filter.FromDate.Value);
                    othersQuery = othersQuery.Where(x => x.PrescriptionDate >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    employeeQuery = employeeQuery.Where(x => x.PrescriptionDate <= filter.ToDate.Value.AddDays(1));
                    othersQuery = othersQuery.Where(x => x.PrescriptionDate <= filter.ToDate.Value.AddDays(1));
                }

                // Execute queries
                var employeeResults = await employeeQuery.ToListAsync();
                var othersResults = await othersQuery.ToListAsync();

                // Combine and group results
                var combinedResults = employeeResults
                    .Concat(othersResults)
                    .GroupBy(x => new { x.MedItemId, x.MedItemName, x.CompanyName, x.BaseName, x.CreatedBy })
                    .Select(g => new DiseaseTrendMedicineWiseViewModel
                    {
                        MedicineId = g.Key.MedItemId,
                        MedicineName = g.Key.MedItemName,
                        CompanyName = g.Key.CompanyName,
                        BaseName = g.Key.BaseName,
                        QuantityUsed = (int)g.Sum(x => x.Quantity),
                        CreatedBy = g.Key.CreatedBy ?? "System"
                    })
                    .OrderByDescending(x => x.QuantityUsed)
                    .ToList();

                // Add serial numbers
                int slNo = 1;
                foreach (var item in combinedResults)
                {
                    item.SlNo = slNo++;
                }

                response.Data = combinedResults;
                response.Summary = new DiseaseTrendMedicineWiseSummary
                {
                    TotalRecords = combinedResults.Count,
                    TotalMedicines = combinedResults.Select(x => x.MedicineId).Distinct().Count(),
                    TotalQuantityUsed = combinedResults.Sum(x => x.QuantityUsed),
                    TotalPrescribers = combinedResults.Select(x => x.CreatedBy).Distinct().Count()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetDiseaseTrendMedicineWiseAsync: {ex.Message}");
            }

            return response;
        }

        #endregion

        #region Export Methods

        public async Task<byte[]> ExportAgeWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null)
        {
            var data = await GetDiseaseTrendAgeWiseAsync(filter, userPlantId, currentUser);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Disease Trend Age Wise");

            // Header
            worksheet.Cell(1, 1).Value = "Unit: ITC LIMITED - PSPD-" + data.ReportInfo.PlantCode;
            worksheet.Range(1, 1, 1, 4).Merge();

            worksheet.Cell(2, 1).Value = "Run Date & Time: " + data.ReportInfo.GeneratedOn;
            worksheet.Cell(2, 3).Value = "Generated By: " + data.ReportInfo.GeneratedBy;

            worksheet.Cell(4, 1).Value = "DISEASE TREND ANALYSIS AGE WISE REPORT";
            worksheet.Range(4, 1, 4, 4).Merge();
            worksheet.Cell(4, 1).Style.Font.Bold = true;
            worksheet.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Column Headers
            worksheet.Cell(6, 1).Value = "SL NO";
            worksheet.Cell(6, 2).Value = "DISEASE NAME";
            worksheet.Cell(6, 3).Value = "COUNT OF EMP";
            worksheet.Cell(6, 4).Value = "AGE GROUP";

            var headerRange = worksheet.Range(6, 1, 6, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Data rows
            int row = 7;
            foreach (var item in data.Data)
            {
                worksheet.Cell(row, 1).Value = item.SlNo;
                worksheet.Cell(row, 2).Value = item.DiseaseName;
                worksheet.Cell(row, 3).Value = item.EmployeeCount;
                worksheet.Cell(row, 4).Value = item.AgeGroup;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> ExportDeptWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null)
        {
            var data = await GetDiseaseTrendDeptWiseAsync(filter, userPlantId, currentUser);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Disease Trend Dept Wise");

            // Header
            worksheet.Cell(1, 1).Value = "Unit: ITC LIMITED - PSPD-" + data.ReportInfo.PlantCode;
            worksheet.Range(1, 1, 1, 4).Merge();

            worksheet.Cell(2, 1).Value = "Run Date & Time: " + data.ReportInfo.GeneratedOn;
            worksheet.Cell(2, 3).Value = "Generated By: " + data.ReportInfo.GeneratedBy;

            worksheet.Cell(4, 1).Value = "DISEASE TREND ANALYSIS DEPARTMENT WISE";
            worksheet.Range(4, 1, 4, 4).Merge();
            worksheet.Cell(4, 1).Style.Font.Bold = true;
            worksheet.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Column Headers
            worksheet.Cell(6, 1).Value = "SL NO";
            worksheet.Cell(6, 2).Value = "DISEASE NAME";
            worksheet.Cell(6, 3).Value = "EMPLOYEE COUNT";
            worksheet.Cell(6, 4).Value = "DEPARTMENT";

            var headerRange = worksheet.Range(6, 1, 6, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Data rows
            int row = 7;
            foreach (var item in data.Data)
            {
                worksheet.Cell(row, 1).Value = item.SlNo;
                worksheet.Cell(row, 2).Value = item.DiseaseName;
                worksheet.Cell(row, 3).Value = item.EmployeeCount;
                worksheet.Cell(row, 4).Value = item.DepartmentName;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> ExportPatientWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null)
        {
            var data = await GetDiseaseTrendPatientWiseAsync(filter, userPlantId, currentUser, 1, int.MaxValue);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Disease Trend Patient Wise");

            // Header
            worksheet.Cell(1, 1).Value = "Unit: ITC LIMITED - PSPD-" + data.ReportInfo.PlantCode;
            worksheet.Range(1, 1, 1, 6).Merge();

            worksheet.Cell(2, 1).Value = "Run Date & Time: " + data.ReportInfo.GeneratedOn;
            worksheet.Cell(2, 4).Value = "Generated By: " + data.ReportInfo.GeneratedBy;

            worksheet.Cell(4, 1).Value = "DISEASE TREND ANALYSIS PATIENT WISE";
            worksheet.Range(4, 1, 4, 6).Merge();
            worksheet.Cell(4, 1).Style.Font.Bold = true;
            worksheet.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Column Headers
            worksheet.Cell(6, 1).Value = "SNO";
            worksheet.Cell(6, 2).Value = "EMPNO";
            worksheet.Cell(6, 3).Value = "PATIENT NAME";
            worksheet.Cell(6, 4).Value = "DISEASE NAME";
            worksheet.Cell(6, 5).Value = "MEDICINE NAME";
            worksheet.Cell(6, 6).Value = "DATE TIME VISIT";

            var headerRange = worksheet.Range(6, 1, 6, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Data rows
            int row = 7;
            foreach (var item in data.Data)
            {
                worksheet.Cell(row, 1).Value = item.SlNo;
                worksheet.Cell(row, 2).Value = item.EmpNo;
                worksheet.Cell(row, 3).Value = item.PatientName;
                worksheet.Cell(row, 4).Value = item.DiseaseName;
                worksheet.Cell(row, 5).Value = item.MedicineName;
                worksheet.Cell(row, 6).Value = item.DateTimeVisitFormatted;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> ExportMedicineWiseToExcelAsync(DiseaseTrendFilterModel filter, int? userPlantId = null, string? currentUser = null)
        {
            var data = await GetDiseaseTrendMedicineWiseAsync(filter, userPlantId, currentUser);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Medicines Consumption");

            // Header
            worksheet.Cell(1, 1).Value = "Unit: ITC LIMITED - PSPD-" + data.ReportInfo.PlantCode;
            worksheet.Range(1, 1, 1, 4).Merge();

            worksheet.Cell(2, 1).Value = "Run Date & Time: " + data.ReportInfo.GeneratedOn;
            worksheet.Cell(2, 3).Value = "Generated By: " + data.ReportInfo.GeneratedBy;

            worksheet.Cell(4, 1).Value = "MEDICINES CONSUMPTION";
            worksheet.Range(4, 1, 4, 4).Merge();
            worksheet.Cell(4, 1).Style.Font.Bold = true;
            worksheet.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Column Headers
            worksheet.Cell(6, 1).Value = "SNO";
            worksheet.Cell(6, 2).Value = "MEDICINE NAME";
            worksheet.Cell(6, 3).Value = "QUANTITY USED";
            worksheet.Cell(6, 4).Value = "CREATEDBY";

            var headerRange = worksheet.Range(6, 1, 6, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Data rows
            int row = 7;
            foreach (var item in data.Data)
            {
                worksheet.Cell(row, 1).Value = item.SlNo;
                worksheet.Cell(row, 2).Value = item.MedicineName;
                worksheet.Cell(row, 3).Value = item.QuantityUsed;
                worksheet.Cell(row, 4).Value = item.CreatedBy;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        #endregion
    }
}
