using EMS.WebApp.Data;
using EMS.WebApp.Data.Migrations;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class DoctorDiagnosisRepository : IDoctorDiagnosisRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly ICompounderIndentRepository _compounderIndentRepo;
        private readonly IEncryptionService _encryptionService;

        public DoctorDiagnosisRepository(
            ApplicationDbContext db,
            ICompounderIndentRepository compounderIndentRepo,
            IEncryptionService encryptionService)
        {
            _db = db;
            _compounderIndentRepo = compounderIndentRepo;
            _encryptionService = encryptionService;
        }

        public async Task<string?> GetUserPlantCodeAsync(string userName)
        {
            try
            {
                var user = await _db.SysUsers
                    .Include(u => u.OrgPlant) // Include OrgPlant navigation property
                    .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

                return user?.OrgPlant?.plant_code;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting plant code for user {userName}: {ex.Message}");
                return null;
            }
        }

        // NEW: Get plant code by plant ID
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
        public async Task<HrEmployee?> GetEmployeeByEmpIdAsync(string empId, int? userPlantId = null)
        {
            var query = _db.HrEmployees
                .Include(e => e.org_department)
                .Include(e => e.org_plant)
                .Where(e => e.emp_id.ToLower() == empId.ToLower());

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<RefMedCondition>> GetMedicalConditionsAsync()
        {
            return await _db.RefMedConditions.ToListAsync();
        }

        // UPDATED: Plant-wise exam filtering
        public async Task<List<int>> GetEmployeeSelectedConditionsAsync(int empUid, DateTime examDate, int? userPlantId = null)
        {
            var examDateOnly = DateOnly.FromDateTime(examDate.Date);

            var query = _db.MedExamHeaders
                .Where(e => e.emp_uid == empUid &&
                           e.exam_date.HasValue &&
                           e.exam_date.Value == examDateOnly);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            var exam = await query.FirstOrDefaultAsync();

            if (exam == null)
                return new List<int>();

            // Get selected condition IDs for this exam
            return await _db.MedExamConditions
                .Where(c => c.exam_id == exam.exam_id)
                .Select(c => c.cond_uid)
                .ToListAsync();
        }

        public async Task<List<MedDisease>> GetDiseasesAsync(int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Getting diseases for plant: {userPlantId}");

                var query = _db.MedDiseases.AsQueryable();

                // Plant-wise filtering for diseases
                if (userPlantId.HasValue)
                {
                    // Option 1: If diseases have direct plant relationship
                    query = query.Where(d => d.plant_id == userPlantId.Value);

                }

                var diseases = await query
                    .OrderBy(d => d.DiseaseName)
                    .ToListAsync();

                Console.WriteLine($"✅ Found {diseases.Count} diseases for plant {userPlantId}");
                return diseases;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting diseases for plant {userPlantId}: {ex.Message}");
                // Return all diseases as fallback
                return await _db.MedDiseases
                    .OrderBy(d => d.DiseaseName)
                    .ToListAsync();
            }
        }

        public async Task<List<MedMaster>> GetMedicinesAsync()
        {
            return await _db.med_masters
                .Include(m => m.MedBase)
                .OrderBy(m => m.MedItemName)
                .ToListAsync();
        }

        // UPDATED: Plant-wise employee search
        public async Task<List<string>> SearchEmployeeIdsAsync(string term, int? userPlantId = null)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<string>();

            var query = _db.HrEmployees
                .Where(e => e.emp_id.ToLower().Contains(term.ToLower()));

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.plant_id == userPlantId.Value);
            }

            return await query
                .OrderBy(e => e.emp_id)
                .Select(e => e.emp_id)
                .Take(10)
                .ToListAsync();
        }

        // UPDATED: Plant-wise diagnosis filtering
        public async Task<List<DiagnosisEntry>> GetEmployeeDiagnosesAsync(string empId, int? userPlantId = null)
        {
            var employee = await GetEmployeeByEmpIdAsync(empId, userPlantId);
            if (employee == null)
                return new List<DiagnosisEntry>();

            var query = _db.MedPrescriptions
                .Include(p => p.PrescriptionDiseases)
                    .ThenInclude(pd => pd.MedDisease)
                .Where(p => p.emp_uid == employee.emp_uid);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(p => p.PlantId == userPlantId.Value);
            }

            var prescriptions = await query
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();

            return prescriptions.Select(p => new DiagnosisEntry
            {
                DiagnosisId = p.PrescriptionId,
                DiagnosisName = p.PrescriptionDiseases.Any()
                    ? string.Join(", ", p.PrescriptionDiseases.Select(pd => pd.MedDisease?.DiseaseName))
                    : "General Consultation",
                LastVisitDate = p.PrescriptionDate,
                EmpId = empId
            }).ToList();
        }

        // UPDATED: Plant-wise medicine stock filtering
        //public async Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(int? userPlantId = null)
        //{
        //    try
        //    {
        //        Console.WriteLine($"🔍 Getting medicines from compounder indent with FIFO logic for plant: {userPlantId}");

        //        var query = _db.CompounderIndentItems
        //            .Include(i => i.MedMaster)
        //                .ThenInclude(m => m.MedBase)
        //            .Include(i => i.CompounderIndent)
        //            .AsQueryable();

        //        // Plant-wise filtering for compounder indents
        //        if (userPlantId.HasValue)
        //        {
        //            query = query.Where(i => i.CompounderIndent.plant_id == userPlantId.Value);
        //        }

        //        var medicineStocks = query
        //            .SelectMany(i => _db.CompounderIndentBatches
        //                .Where(b => b.IndentItemId == i.IndentItemId &&
        //                           i.ReceivedQuantity > 0 &&
        //                           b.AvailableStock > 0 &&
        //                           !string.IsNullOrEmpty(b.BatchNo) &&
        //                           b.ExpiryDate >= DateTime.Today)
        //                .Select(b => new MedicineStockInfo
        //                {
        //                    IndentItemId = i.IndentItemId, // FIXED: Keep actual IndentItemId for each batch
        //                    MedItemId = i.MedItemId,
        //                    MedItemName = i.MedMaster.MedItemName,
        //                    CompanyName = i.MedMaster.CompanyName ?? "Not Defined",
        //                    BatchNo = b.BatchNo,
        //                    ExpiryDate = b.ExpiryDate,
        //                    AvailableStock = b.AvailableStock, // FIXED: Actual stock for this specific batch
        //                    BaseName = i.MedMaster.MedBase != null
        //                        ? i.MedMaster.MedBase.BaseName
        //                        : "Not Defined",
        //                    PlantId = i.CompounderIndent.plant_id
        //                }))
        //            .OrderBy(m => m.MedItemName) // Order by medicine name
        //            .ThenBy(m => m.BatchNo) // Then by batch
        //            .ThenBy(m => m.ExpiryDate ?? DateTime.MaxValue) // Then by expiry (FIFO)
        //            .ToList();

        //        Console.WriteLine($"✅ Found {medicineStocks.Count} individual medicine batches with available stock for plant {userPlantId}");

        //        return medicineStocks;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error getting medicines from compounder indent: {ex.Message}");

        //        // Enhanced fallback with plant filtering
        //        var fallbackQuery = _db.med_masters.AsQueryable();
        //        var fallbackMedicines = await fallbackQuery.ToListAsync();

        //        return fallbackMedicines.Select(m => new MedicineStockInfo
        //        {
        //            IndentItemId = 0,
        //            MedItemId = m.MedItemId,
        //            MedItemName = m.MedItemName,
        //            CompanyName = m.CompanyName ?? "Not Defined",
        //            BatchNo = "FALLBACK",
        //            ExpiryDate = DateTime.Now.AddYears(1),
        //            AvailableStock = 999,
        //            BaseName = "Not Defined",
        //            PlantId = userPlantId ?? 1
        //        }).ToList();
        //    }
        //}

        public async Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(
            int? userPlantId = null,
            string? currentUser = null,
            bool isDoctor = false)
        {
            try
            {

                var query = _db.CompounderIndentItems
                    .Include(i => i.MedMaster)
                        .ThenInclude(m => m.MedBase)
                    .Include(i => i.CompounderIndent)
                    .AsQueryable();

                // Plant-wise filtering for compounder indents
                if (userPlantId.HasValue)
                {
                    query = query.Where(i => i.CompounderIndent.plant_id == userPlantId.Value);

                    if (!isDoctor)
                    {
                        var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                        if (plantCode?.ToUpper() == "BCM")
                        {
                            Console.WriteLine($"🔒 BCM Plant detected - Filtering medicines for user: {currentUser}");

                            if (!string.IsNullOrEmpty(currentUser))
                            {
                                // BCM plant: Non-doctors can only see medicines from indents they created
                                query = query.Where(i => i.CompounderIndent.CreatedBy == currentUser);
                            }
                            else
                            {
                                // Safety: If no current user, return empty list
                                Console.WriteLine($"⚠️ BCM Plant but no current user - returning empty list");
                                return new List<MedicineStockInfo>();
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"👨‍⚕️ Doctor user - showing all medicines regardless of creator");
                    }
                }

                var medicineStocks = query
                    .SelectMany(i => _db.CompounderIndentBatches
                        .Where(b => b.IndentItemId == i.IndentItemId &&
                                   i.ReceivedQuantity > 0 &&
                                   b.AvailableStock > 0 &&
                                   !string.IsNullOrEmpty(b.BatchNo) &&
                                   b.ExpiryDate >= DateTime.Today)
                        .Select(b => new MedicineStockInfo
                        {
                            IndentItemId = i.IndentItemId,
                            MedItemId = i.MedItemId,
                            MedItemName = i.MedMaster.MedItemName,
                            CompanyName = i.MedMaster.CompanyName ?? "Not Defined",
                            BatchNo = b.BatchNo,
                            ExpiryDate = b.ExpiryDate,
                            AvailableStock = b.AvailableStock,
                            BaseName = i.MedMaster.MedBase != null
                                ? i.MedMaster.MedBase.BaseName
                                : "Not Defined",
                            PlantId = i.CompounderIndent.plant_id
                        }))
                    .OrderBy(m => m.MedItemName)
                    .ThenBy(m => m.BatchNo)
                    .ThenBy(m => m.ExpiryDate ?? DateTime.MaxValue)
                    .ToList();

                Console.WriteLine($"✅ Found {medicineStocks.Count} individual medicine batches for plant {userPlantId}");

                return medicineStocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting medicines from compounder indent: {ex.Message}");

                // Enhanced fallback with plant filtering
                var fallbackQuery = _db.med_masters.AsQueryable();
                var fallbackMedicines = await fallbackQuery.ToListAsync();

                return fallbackMedicines.Select(m => new MedicineStockInfo
                {
                    IndentItemId = 0,
                    MedItemId = m.MedItemId,
                    MedItemName = m.MedItemName,
                    CompanyName = m.CompanyName ?? "Not Defined",
                    BatchNo = "FALLBACK",
                    ExpiryDate = DateTime.Now.AddYears(1),
                    AvailableStock = 999,
                    BaseName = "Not Defined",
                    PlantId = userPlantId ?? 1
                }).ToList();
            }
        }

        // UPDATED: Plant-wise stock checking

        public async Task<int> GetAvailableStockAsync(int indentItemId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Getting available stock for IndentItemId {indentItemId} using FIFO logic (Plant: {userPlantId})");

                var query = from batch in _db.CompounderIndentBatches
                            join item in _db.CompounderIndentItems on batch.IndentItemId equals item.IndentItemId
                            join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                            where batch.IndentItemId == indentItemId && batch.AvailableStock > 0
                            select new
                            {
                                batch.AvailableStock,
                                batch.ExpiryDate,
                                batch.BatchNo,
                                indent.plant_id
                            };

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(x => x.plant_id == userPlantId.Value);
                }

                // FIXED: Use FIFO logic - order by expiry date (earliest first)
                var result = await query
                    .OrderBy(x => x.ExpiryDate) // FIFO: earliest expiry first
                    .ThenBy(x => x.BatchNo) // Then by batch number for consistency
                    .FirstOrDefaultAsync();

                if (result != null)
                {
                    Console.WriteLine($"✅ FIFO Stock found for IndentItemId {indentItemId}: {result.AvailableStock} units (Batch: {result.BatchNo}, Expiry: {result.ExpiryDate.ToString("dd/MM/yyyy") ?? "N/A"})");
                    return result.AvailableStock;
                }

                Console.WriteLine($"⚠️ No available stock found for IndentItemId {indentItemId} in plant {userPlantId}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting available stock for item {indentItemId}: {ex.Message}");
                return 0;
            }
        }



        // UPDATED: Plant-wise stock updates

        public async Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔄 Updating available stock for IndentItemId {indentItemId}, using {quantityUsed} units with FIFO logic (Plant: {userPlantId})");

                var query = from cib in _db.CompounderIndentBatches
                            join item in _db.CompounderIndentItems on cib.IndentItemId equals item.IndentItemId
                            join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                            where cib.IndentItemId == indentItemId && cib.AvailableStock > 0
                            select new
                            {
                                Batch = cib,
                                indent.plant_id,
                                cib.ExpiryDate,
                                cib.BatchNo
                            };

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(x => x.plant_id == userPlantId.Value);
                }

                // FIXED: Use FIFO logic - order by expiry date (earliest first)
                var result = await query
                    .OrderBy(x => x.ExpiryDate) // FIFO: earliest expiry first
                    .ThenBy(x => x.BatchNo) // Then by batch number for consistency
                    .FirstOrDefaultAsync();

                if (result?.Batch == null)
                {
                    Console.WriteLine($"❌ Compounder indent item {indentItemId} not found or no available stock for plant {userPlantId}");
                    return false;
                }

                var batch = result.Batch;
                if (batch.AvailableStock < quantityUsed)
                {
                    Console.WriteLine($"❌ Insufficient stock in FIFO batch. Available: {batch.AvailableStock}, Requested: {quantityUsed} (Batch: {result.BatchNo})");
                    return false;
                }

                var oldStock = batch.AvailableStock;
                batch.AvailableStock = oldStock - quantityUsed;

                _db.CompounderIndentBatches.Update(batch);
                await _db.SaveChangesAsync();

                Console.WriteLine($"✅ FIFO Stock updated for IndentItemId {indentItemId}: {oldStock} → {batch.AvailableStock} (Batch: {result.BatchNo}, Expiry: {result.ExpiryDate.ToString("dd/MM/yyyy") ?? "N/A"}, Plant: {userPlantId})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating available stock for item {indentItemId}: {ex.Message}");
                return false;
            }
        }



        // NEW: Method to get plant code for determining approval requirements
        private async Task<string?> GetPlantCodeAsync(int? plantId)
        {
            try
            {
                if (!plantId.HasValue) return null;

                var plant = await _db.org_plants
                    .FirstOrDefaultAsync(p => p.plant_id == plantId.Value);

                return plant?.plant_code;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting plant code for plant {plantId}: {ex.Message}");
                return null;
            }
        }

        // UPDATED: Plant-wise prescription saving with BCM plant approval logic

        public async Task<bool> SavePrescriptionAsync(string empId, DateTime examDate,
        List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
        VitalSigns vitalSigns, string createdBy, int? userPlantId = null,
        string? visitType = null, string? patientStatus = null, string? dependentName = null, string? userRemarks = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var selectedPatientStatus = patientStatus ?? "On Duty";
                var selectedDependentName = dependentName ?? "Self";

                if (!string.IsNullOrEmpty(visitType) || !string.IsNullOrEmpty(selectedPatientStatus) || selectedDependentName != "Self")
                {
                    var patientInfo = selectedDependentName != "Self" ? $"Dependent: {selectedDependentName}" : "Employee (Self)";
                    Console.WriteLine($"🏥 Processing {visitType} for employee {empId} by {createdBy} - Patient: {patientInfo}, Patient Status: {selectedPatientStatus} (Plant: {userPlantId})");
                }

                var employee = await GetEmployeeByEmpIdAsync(empId, userPlantId);
                if (employee == null)
                {
                    throw new InvalidOperationException($"Employee with ID '{empId}' not found in plant {userPlantId}.");
                }

                // NEW: Validate dependent if specified and get dependent details
                HrEmployeeDependent? dependentDetails = null;
                if (selectedDependentName != "Self")
                {
                    dependentDetails = await _db.HrEmployeeDependents
                        .FirstOrDefaultAsync(d => d.emp_uid == employee.emp_uid &&
                                                  d.dep_name == selectedDependentName &&
                                                  d.is_active);

                    if (dependentDetails == null)
                    {
                        throw new InvalidOperationException($"Dependent '{selectedDependentName}' not found for employee '{empId}'.");
                    }
                }

                var employeePlantId = employee.plant_id;
                var examDateOnly = DateOnly.FromDateTime(examDate.Date);

                // Find or create exam header with plant info
                var examQuery = _db.MedExamHeaders
                    .Where(e => e.emp_uid == employee.emp_uid &&
                               e.exam_date.HasValue &&
                               e.exam_date.Value == examDateOnly);

                if (userPlantId.HasValue)
                {
                    examQuery = examQuery.Where(e => e.PlantId == userPlantId.Value);
                }

                var exam = await examQuery.FirstOrDefaultAsync();

                if (exam == null)
                {
                    exam = new MedExamHeader
                    {
                        emp_uid = employee.emp_uid,
                        exam_date = examDateOnly,
                        PlantId = (short)employeePlantId
                    };
                    _db.MedExamHeaders.Add(exam);
                    await _db.SaveChangesAsync(); // Save to get the exam_id
                }

                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);
                            if (availableStock < medicine.Quantity)
                            {
                                Console.WriteLine($"❌ Stock validation failed for Medicine ID {medicine.MedItemId}: {medicine.MedicineName} (Plant: {userPlantId}) (IndentItemID: {medicine.IndentItemId.Value})");
                                throw new InvalidOperationException($"Insufficient stock for {medicine.MedicineName} (ID: {medicine.MedItemId}) in your plant. Available: {availableStock}, Requested: {medicine.Quantity} (IndentItemID: {medicine.IndentItemId.Value})");
                            }
                        }
                    }
                }

                // Enhanced approval logic for BCM plant
                var plantCode = await GetPlantCodeAsync(employeePlantId);
                string approvalStatus;
                string? approvedBy = null;
                DateTime? approvedDate = null;

                if (plantCode?.ToUpper() == "BCM" || visitType == "First Aid or Emergency")
                {
                    approvalStatus = "Pending";
                }
                else
                {
                    approvalStatus = "Approved";
                    approvedBy = createdBy;
                    approvedDate = DateTime.Now;
                }

                var remarks = new List<string>();
                // NEW: Add user-provided remarks first if provided
                if (!string.IsNullOrEmpty(userRemarks))
                {
                    remarks.Add($"User Remarks: {userRemarks}");
                }

                if (!string.IsNullOrEmpty(visitType))
                {
                    remarks.Add($"Visit Type: {visitType}");
                }
                if (!string.IsNullOrEmpty(selectedPatientStatus))
                {
                    remarks.Add($"Patient Status: {selectedPatientStatus}");
                }

                // UPDATED: Add dependent information to remarks with relation
                if (!string.IsNullOrEmpty(selectedDependentName) && selectedDependentName != "Self")
                {
                    if (dependentDetails != null)
                    {
                        remarks.Add($"Patient: Dependent - {selectedDependentName}; Relation: {dependentDetails.relation ?? "Not Specified"}");
                    }
                    else
                    {
                        remarks.Add($"Patient: Dependent - {selectedDependentName}; Relation: Not Specified");
                    }
                }
                else
                {
                    remarks.Add($"Patient: Employee (Self)");
                }
                var remarksString = string.Join("; ", remarks);

                // Create prescription record
                var prescription = new MedPrescription
                {
                    emp_uid = employee.emp_uid,
                    exam_id = exam.exam_id,
                    PrescriptionDate = examDate.Date.Add(DateTime.Now.TimeOfDay),
                    BloodPressure = _encryptionService.Encrypt(vitalSigns.BloodPressure),
                    Pulse = _encryptionService.Encrypt(vitalSigns.Pulse),
                    Temperature = _encryptionService.Encrypt(vitalSigns.Temperature),
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now,
                    Remarks = remarksString,
                    PatientStatus = selectedPatientStatus,
                    ApprovalStatus = approvalStatus,
                    ApprovedBy = approvedBy,
                    ApprovedDate = approvedDate,
                    PlantId = (short)employeePlantId,
                    // Store dependent name (you might need to add this field to MedPrescription table)
                    DependentName = selectedDependentName != "Self" ? selectedDependentName : null
                };

                _db.MedPrescriptions.Add(prescription);
                await _db.SaveChangesAsync(); // Save to get the PrescriptionId

                // Save prescription diseases using the generated PrescriptionId
                if (selectedDiseases?.Any() == true)
                {
                    var prescriptionDiseases = new List<MedPrescriptionDisease>();
                    foreach (var diseaseId in selectedDiseases)
                    {
                        prescriptionDiseases.Add(new MedPrescriptionDisease
                        {
                            PrescriptionId = prescription.PrescriptionId,
                            DiseaseId = diseaseId
                        });
                    }

                    _db.MedPrescriptionDiseases.AddRange(prescriptionDiseases);
                    Console.WriteLine($"✅ Prepared {prescriptionDiseases.Count} diseases for prescription {prescription.PrescriptionId}");
                }

                // Save prescription medicines using the generated PrescriptionId
                if (medicines?.Any() == true)
                {
                    var prescriptionMedicines = new List<MedPrescriptionMedicine>();

                    // Group medicines by MedItemId to handle multiple batches
                    var medicineGroups = medicines.GroupBy(m => m.MedItemId).ToList();

                    foreach (var group in medicineGroups)
                    {
                        foreach (var med in group)
                        {
                            prescriptionMedicines.Add(new MedPrescriptionMedicine
                            {
                                PrescriptionId = prescription.PrescriptionId,
                                MedItemId = med.MedItemId,
                                Quantity = med.Quantity,
                                Dose = med.Dose,
                                Instructions = $"{_encryptionService.Encrypt($"ID:{med.MedItemId} - {med.MedicineName}")} - {med.Dose}"
                            });
                        }
                    }

                    _db.MedPrescriptionMedicines.AddRange(prescriptionMedicines);

                    // Update available stock for each medicine batch
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var stockUpdated = await UpdateAvailableStockAsync(medicine.IndentItemId.Value, medicine.Quantity, userPlantId);
                            if (!stockUpdated)
                            {
                                throw new InvalidOperationException($"Failed to update stock for Medicine ID {medicine.MedItemId}: {medicine.MedicineName} in plant {userPlantId}");
                            }
                        }
                    }

                    Console.WriteLine($"✅ Prepared {prescriptionMedicines.Count} medicines for prescription {prescription.PrescriptionId}");
                }

                // Save all child records together
                await _db.SaveChangesAsync();

                // Update or create general exam
                var generalExamQuery = _db.MedGeneralExams
                    .Where(g => g.exam_id == exam.exam_id);

                if (userPlantId.HasValue)
                {
                    generalExamQuery = generalExamQuery.Where(g => g.PlantId == userPlantId.Value);
                }

                var generalExam = await generalExamQuery.FirstOrDefaultAsync();

                if (generalExam != null)
                {
                    generalExam.bp = _encryptionService.Encrypt(vitalSigns.BloodPressure);
                    generalExam.pulse = _encryptionService.Encrypt(vitalSigns.Pulse);
                    _db.MedGeneralExams.Update(generalExam);
                }
                else
                {
                    generalExam = new MedGeneralExam
                    {
                        emp_uid = employee.emp_uid,
                        exam_id = exam.exam_id,
                        bp = _encryptionService.Encrypt(vitalSigns.BloodPressure),
                        pulse = _encryptionService.Encrypt(vitalSigns.Pulse),
                        PlantId = (short)employeePlantId
                    };
                    _db.MedGeneralExams.Add(generalExam);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                var patientDisplay = selectedDependentName != "Self" ?
                    $"Dependent: {selectedDependentName} ({dependentDetails?.relation ?? "Unknown Relation"})" :
                    "Employee (Self)";
                Console.WriteLine($"✅ Prescription saved successfully for employee {empId} - Patient: {patientDisplay}, Status: {approvalStatus}, Patient Status: {selectedPatientStatus}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error saving prescription for employee {empId}: {ex.Message}");
                throw;
            }
        }
        // UPDATED: Plant-wise prescription details with PatientStatus
        public async Task<PrescriptionDetailsViewModel?> GetPrescriptionDetailsAsync(int prescriptionId, int? userPlantId = null)
        {
            try
            {
                var query = _db.MedPrescriptions
                    .Include(p => p.HrEmployee)
                    .ThenInclude(e => e.org_department)
                    .Include(p => p.HrEmployee)
                    .ThenInclude(e => e.org_plant)
                    .Include(p => p.PrescriptionDiseases)
                        .ThenInclude(pd => pd.MedDisease)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                        .ThenInclude(s => s.MedBase)
                    .Include(p => p.OrgPlant)
                    .Where(p => p.PrescriptionId == prescriptionId);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                    return null;

                var result = new PrescriptionDetailsViewModel
                {
                    PrescriptionId = prescription.PrescriptionId,
                    EmployeeId = prescription.HrEmployee?.emp_id ?? "N/A",
                    EmployeeName = prescription.HrEmployee?.emp_name ?? "N/A",
                    Department = prescription.HrEmployee?.org_department?.dept_name ?? "N/A",
                    Plant = prescription.OrgPlant?.plant_name ?? "N/A",
                    PrescriptionDate = prescription.PrescriptionDate,
                    CreatedBy = prescription.CreatedBy,
                    BloodPressure = _encryptionService.Decrypt(prescription.BloodPressure),
                    Pulse = _encryptionService.Decrypt(prescription.Pulse),
                    Temperature = _encryptionService.Decrypt(prescription.Temperature),
                    Remarks = prescription.Remarks,
                    PatientStatus = prescription.PatientStatus, // NEW: Include patient status
                    Diseases = prescription.PrescriptionDiseases?.Select(pd => new PrescriptionDiseaseDetails
                    {
                        DiseaseId = pd.DiseaseId,
                        DiseaseName = pd.MedDisease?.DiseaseName ?? "Unknown Disease",
                        DiseaseDescription = pd.MedDisease?.DiseaseDesc
                    }).ToList() ?? new List<PrescriptionDiseaseDetails>(),
                    Medicines = prescription.PrescriptionMedicines?.Select(pm =>
                    {
                        var medicineName = pm.MedMaster?.MedItemName ?? "Unknown Medicine";
                        var medicineBase = pm.MedMaster?.MedBase?.BaseName ?? "Not Defined";

                        // Improved decryption logic
                        if (!string.IsNullOrEmpty(pm.Instructions))
                        {
                            try
                            {


                                if (!string.IsNullOrEmpty(pm.Instructions) && pm.Instructions.Contains(" - "))
                                {
                                    var parts = pm.Instructions.Split(" - ", 2);
                                    if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                                    {
                                        var decryptedName = _encryptionService.Decrypt(parts[0]);
                                        if (!string.IsNullOrEmpty(decryptedName))
                                        {
                                            medicineBase = decryptedName.Replace("ID:", "");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error decrypting medicine name for prescription {prescriptionId}: {ex.Message}");
                                // Keep the original medicine name from MedMaster
                            }
                        }

                        return new PrescriptionMedicineDetails
                        {
                            MedItemId = pm.MedItemId,
                            MedicineName = medicineName,
                            BaseName = medicineBase,
                            Quantity = pm.Quantity,
                            Dose = pm.Dose,
                            Instructions = pm.Instructions,
                            CompanyName = pm.MedMaster?.CompanyName
                        };
                    }).ToList() ?? new List<PrescriptionMedicineDetails>()
                };

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting prescription details for ID {prescriptionId}: {ex.Message}");
                return null;
            }
        }
        // UPDATED: Plant-wise pending approval count
        public async Task<int> GetPendingApprovalCountAsync(int? userPlantId = null)
        {
            try
            {
                var query = _db.MedPrescriptions
                    .Where(p => p.ApprovalStatus == "Pending");

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var count = await query.CountAsync();

                Console.WriteLine($"📊 Pending approval count for plant {userPlantId}: {count}");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting pending approval count: {ex.Message}");
                return 0;
            }
        }

        // UPDATED: Plant-wise pending approvals
        public async Task<List<PendingApprovalViewModel>> GetPendingApprovalsAsync(int? userPlantId = null)
        {
            try
            {
                var query = _db.MedPrescriptions
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_department)
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_plant)
                    .Include(p => p.PrescriptionDiseases)
                        .ThenInclude(pd => pd.MedDisease)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                        .ThenInclude(s => s.MedBase)
                    .Include(p => p.OrgPlant)
                    .Where(p => p.ApprovalStatus == "Pending");

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var pendingPrescriptions = await query
                    .OrderByDescending(p => p.PrescriptionDate)      // Primary: By date (newest first)
                    .ThenByDescending(p => p.CreatedDate)            // Secondary: By creation time
                    .ThenBy(p => p.HrEmployee.emp_name)              // Tertiary: By employee name for readability
                    .ToListAsync();

                Console.WriteLine($"📋 Found {pendingPrescriptions.Count} pending prescriptions for approval in plant {userPlantId}");

                return pendingPrescriptions.Select(p => new PendingApprovalViewModel
                {
                    PrescriptionId = p.PrescriptionId,
                    EmployeeId = p.HrEmployee?.emp_id ?? "N/A",
                    EmployeeName = p.HrEmployee?.emp_name ?? "N/A",
                    Department = p.HrEmployee?.org_department?.dept_name ?? "N/A",
                    Plant = p.OrgPlant?.plant_name ?? "N/A",
                    PrescriptionDate = p.PrescriptionDate,
                    VisitType = ExtractVisitTypeFromRemarks(p.Remarks) ?? "First Aid or Emergency",
                    CreatedBy = p.CreatedBy,
                    BloodPressure = _encryptionService.Decrypt(p.BloodPressure),
                    Pulse = _encryptionService.Decrypt(p.Pulse),
                    Temperature = _encryptionService.Decrypt(p.Temperature),
                    ApprovalStatus = p.ApprovalStatus,
                    MedicineCount = p.PrescriptionMedicines?.Count ?? 0,
                    PatientStatus = p.PatientStatus ?? "On Duty",
                    Remarks = p.Remarks,

                    // NEW: Extract dependent name from remarks or use database field if available
                    DependentName = ExtractDependentNameFromRemarks(p.Remarks) ?? p.DependentName ?? "Self",

                    Diseases = p.PrescriptionDiseases?.Select(pd => new PrescriptionDiseaseDetails
                    {
                        DiseaseId = pd.DiseaseId,
                        DiseaseName = pd.MedDisease?.DiseaseName ?? "Unknown Disease",
                        DiseaseDescription = pd.MedDisease?.DiseaseDesc
                    }).ToList() ?? new List<PrescriptionDiseaseDetails>(),

                    Medicines = p.PrescriptionMedicines?.Select(pm =>
                    {
                        var medicineName = pm.MedMaster?.MedItemName ?? "Unknown Medicine";
                        var medicineBase = pm.MedMaster?.MedBase?.BaseName ?? "Not Defined";

                        // Improved decryption logic for pending approvals
                        if (!string.IsNullOrEmpty(pm.Instructions))
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(pm.Instructions) && pm.Instructions.Contains(" - "))
                                {
                                    var parts = pm.Instructions.Split(" - ", 2);
                                    if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                                    {
                                        var decryptedName = _encryptionService.Decrypt(parts[0]);
                                        if (!string.IsNullOrEmpty(decryptedName))
                                        {
                                            medicineBase = decryptedName.Replace("ID:", "");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error decrypting medicine name for prescription {p.PrescriptionId}: {ex.Message}");
                                // Keep the original medicine name from MedMaster
                            }
                        }

                        return new PrescriptionMedicineDetails
                        {
                            MedItemId = pm.MedItemId,
                            MedicineName = medicineName,
                            BaseName = medicineBase,
                            Quantity = pm.Quantity,
                            Dose = pm.Dose,
                            Instructions = pm.Instructions,
                            CompanyName = pm.MedMaster?.CompanyName
                        };
                    }).ToList() ?? new List<PrescriptionMedicineDetails>()
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting pending approvals: {ex.Message}");
                return new List<PendingApprovalViewModel>();
            }
        }
        // UPDATED: Plant-wise approval
        public async Task<bool> ApprovePrescriptionAsync(int prescriptionId, string approvedBy, int? userPlantId = null)
        {
            try
            {
                var query = _db.MedPrescriptions
                    .Where(p => p.PrescriptionId == prescriptionId && p.ApprovalStatus == "Pending");

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                {
                    Console.WriteLine($"⚠️ Prescription {prescriptionId} not found or not pending in plant {userPlantId}");
                    return false;
                }

                prescription.ApprovalStatus = "Approved";
                prescription.ApprovedBy = approvedBy;
                prescription.ApprovedDate = DateTime.Now;

                _db.MedPrescriptions.Update(prescription);
                await _db.SaveChangesAsync();

                Console.WriteLine($"✅ Prescription {prescriptionId} approved by {approvedBy} in plant {userPlantId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error approving prescription {prescriptionId}: {ex.Message}");
                return false;
            }
        }

        // UPDATED: Plant-wise rejection
        public async Task<bool> RejectPrescriptionAsync(int prescriptionId, string rejectionReason, string rejectedBy, int? userPlantId = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    Console.WriteLine($"⚠️ Rejection reason is required for prescription {prescriptionId}");
                    return false;
                }

                var query = _db.MedPrescriptions
                    .Include(p => p.PrescriptionMedicines) // Include medicines to restore stock
                    .Where(p => p.PrescriptionId == prescriptionId && p.ApprovalStatus == "Pending");

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                {
                    Console.WriteLine($"⚠️ Prescription {prescriptionId} not found or not pending in plant {userPlantId}");
                    return false;
                }

                // IMPORTANT: Restore medicine stock before rejecting
                if (prescription.PrescriptionMedicines?.Any() == true)
                {
                    Console.WriteLine($"🔄 Restoring stock for {prescription.PrescriptionMedicines.Count} medicines due to rejection");

                    foreach (var prescriptionMedicine in prescription.PrescriptionMedicines)
                    {
                        // Find the indent batch to restore stock
                        var indentBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == prescriptionMedicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == prescription.PlantId)
                            .OrderBy(b => b.ExpiryDate) // Use oldest batch first (FIFO)
                            .FirstOrDefaultAsync();

                        if (indentBatch != null)
                        {
                            var oldStock = indentBatch.AvailableStock;
                            indentBatch.AvailableStock += prescriptionMedicine.Quantity;

                            Console.WriteLine($"🔄 Restored stock for Medicine ID {prescriptionMedicine.MedItemId}: {oldStock} → {indentBatch.AvailableStock} (+{prescriptionMedicine.Quantity}) due to rejection");

                            _db.CompounderIndentBatches.Update(indentBatch);
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Could not find batch to restore stock for Medicine ID {prescriptionMedicine.MedItemId} during rejection");
                        }
                    }
                }

                // Update prescription status to rejected
                prescription.ApprovalStatus = "Rejected";
                prescription.ApprovedBy = rejectedBy;
                prescription.ApprovedDate = DateTime.Now;
                prescription.RejectionReason = rejectionReason;

                _db.MedPrescriptions.Update(prescription);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"❌ Prescription {prescriptionId} rejected by {rejectedBy} in plant {userPlantId}. Reason: {rejectionReason}. Stock restored successfully.");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error rejecting prescription {prescriptionId}: {ex.Message}");
                throw; // Re-throw to let controller handle the error
            }
        }
        // UPDATED: Plant-wise bulk approval
        public async Task<int> ApproveAllPrescriptionsAsync(List<int> prescriptionIds, string approvedBy, int? userPlantId = null)
        {
            if (prescriptionIds == null || !prescriptionIds.Any())
            {
                Console.WriteLine($"⚠️ No prescription IDs provided for bulk approval");
                return 0;
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var approvedCount = 0;
                var approvalTimestamp = DateTime.Now;

                var query = _db.MedPrescriptions
                    .Where(p => prescriptionIds.Contains(p.PrescriptionId) && p.ApprovalStatus == "Pending");

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescriptions = await query.ToListAsync();

                Console.WriteLine($"📋 Found {prescriptions.Count} valid prescriptions out of {prescriptionIds.Count} requested for bulk approval in plant {userPlantId}");

                foreach (var prescription in prescriptions)
                {
                    prescription.ApprovalStatus = "Approved";
                    prescription.ApprovedBy = approvedBy;
                    prescription.ApprovedDate = approvalTimestamp;
                    approvedCount++;
                }

                if (approvedCount > 0)
                {
                    _db.MedPrescriptions.UpdateRange(prescriptions);
                    await _db.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                Console.WriteLine($"✅ {approvedCount} prescriptions approved by {approvedBy} in plant {userPlantId}");
                return approvedCount;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error approving multiple prescriptions: {ex.Message}");
                return 0;
            }
        }

        // ======= NEW PLANT-WISE HELPER METHODS =======

        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            try
            {
                var user = await _db.SysUsers
                    .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

                return user?.plant_id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting user plant for {userName}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> IsUserAuthorizedForPrescriptionAsync(int prescriptionId, int userPlantId)
        {
            try
            {
                return await _db.MedPrescriptions.AnyAsync(p => p.PrescriptionId == prescriptionId && p.PlantId == userPlantId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking prescription authorization: {ex.Message}");
                return false;
            }
        }

        public async Task<int?> GetEmployeePlantIdAsync(string empId)
        {
            try
            {
                var employee = await _db.HrEmployees
                    .FirstOrDefaultAsync(e => e.emp_id.ToLower() == empId.ToLower());

                return employee?.plant_id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting employee plant for {empId}: {ex.Message}");
                return null;
            }
        }

        // Helper method to extract visit type from remarks
        private string? ExtractVisitTypeFromRemarks(string? remarks)
        {
            if (string.IsNullOrEmpty(remarks))
                return null;

            if (remarks.Contains("Visit Type: First Aid or Emergency", StringComparison.OrdinalIgnoreCase))
                return "First Aid or Emergency";

            if (remarks.Contains("Visit Type: Regular Visitor", StringComparison.OrdinalIgnoreCase))
                return "Regular Visitor";

            return "First Aid or Emergency";
        }

        public async Task<IEnumerable<EmployeeDiagnosisListViewModel>> GetAllEmployeeDiagnosesAsync(
        int? userPlantId = null,
        string? currentUser = null,
        bool isDoctor = false)
        {
            try
            {
                Console.WriteLine($"🔍 Getting all employee diagnoses - Plant: {userPlantId}, User: {currentUser}, IsDoctor: {isDoctor}");

                var query = _db.MedPrescriptions
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_department)
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_plant)
                    .Include(p => p.PrescriptionDiseases)
                        .ThenInclude(pd => pd.MedDisease)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                    .Include(p => p.OrgPlant)
                    .AsQueryable();

                // Apply plant filtering only if userPlantId is provided and valid
                if (userPlantId.HasValue && userPlantId.Value > 0)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);

                    // ============================================
                    // NEW: BCM PLANT FILTERING LOGIC
                    // ============================================
                    if (!isDoctor)
                    {
                        var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
                        if (plantCode?.ToUpper() == "BCM")
                        {
                            Console.WriteLine($"🔒 BCM Plant detected - Filtering prescriptions for user: {currentUser}");

                            if (!string.IsNullOrEmpty(currentUser))
                            {
                                // BCM plant: Non-doctors can only see prescriptions they created
                                query = query.Where(p => p.CreatedBy == currentUser);
                            }
                            else
                            {
                                // Safety: If no current user, return empty list
                                Console.WriteLine($"⚠️ BCM Plant but no current user - returning empty list");
                                return new List<EmployeeDiagnosisListViewModel>();
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"👨‍⚕️ Doctor user - showing all prescriptions regardless of creator");
                    }
                    // ============================================
                    // END BCM FILTERING
                    // ============================================
                }

                var prescriptions = await query
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ThenByDescending(p => p.CreatedDate)
                    .ThenByDescending(p => p.PrescriptionId)
                    .ToListAsync();

                var result = prescriptions.Select(p => new EmployeeDiagnosisListViewModel
                {
                    PrescriptionId = p.PrescriptionId,
                    EmployeeId = p.HrEmployee?.emp_id ?? "N/A",
                    EmployeeName = p.HrEmployee?.emp_name ?? "N/A",
                    Department = p.HrEmployee?.org_department?.dept_name ?? "N/A",
                    Plant = p.OrgPlant?.plant_name ?? p.HrEmployee?.org_plant?.plant_name ?? "N/A",
                    PrescriptionDate = p.PrescriptionDate,
                    VisitType = ExtractVisitTypeFromRemarks(p.Remarks) ?? "Regular Visitor",
                    ApprovalStatus = p.ApprovalStatus ?? "Completed",
                    CreatedBy = p.CreatedBy ?? "N/A",
                    DiseaseCount = p.PrescriptionDiseases?.Count ?? 0,
                    MedicineCount = p.PrescriptionMedicines?.Count ?? 0,
                    BloodPressure = SafeDecrypt(p.BloodPressure),
                    Pulse = SafeDecrypt(p.Pulse),
                    Temperature = SafeDecrypt(p.Temperature),
                    ApprovedBy = p.ApprovedBy,
                    ApprovedDate = p.ApprovedDate,
                    RejectionReason = p.RejectionReason,
                    Remarks = p.Remarks,
                    PatientStatus = p.PatientStatus ?? "On Duty",
                    DependentName = ExtractDependentNameFromRemarks(p.Remarks) ?? p.DependentName ?? "Self"
                }).ToList();

                Console.WriteLine($"✅ Found {result.Count} employee diagnoses for plant {userPlantId}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting all employee diagnoses: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return new List<EmployeeDiagnosisListViewModel>();
            }
        }
        //public async Task<IEnumerable<EmployeeDiagnosisListViewModel>> GetAllEmployeeDiagnosesAsync(int? userPlantId = null)
        //{
        //    try
        //    {
        //        Console.WriteLine($"🔍 Getting all employee diagnoses for plant: {userPlantId}");

        //        var query = _db.MedPrescriptions
        //            .Include(p => p.HrEmployee)
        //                .ThenInclude(e => e.org_department)
        //            .Include(p => p.HrEmployee)
        //                .ThenInclude(e => e.org_plant)
        //            .Include(p => p.PrescriptionDiseases)
        //                .ThenInclude(pd => pd.MedDisease)
        //            .Include(p => p.PrescriptionMedicines)
        //                .ThenInclude(pm => pm.MedMaster)
        //            .Include(p => p.OrgPlant)
        //            .AsQueryable();

        //        // Apply plant filtering only if userPlantId is provided and valid
        //        if (userPlantId.HasValue && userPlantId.Value > 0)
        //        {
        //            query = query.Where(p => p.PlantId == userPlantId.Value);
        //        }

        //        var prescriptions = await query
        //            .OrderByDescending(p => p.PrescriptionDate)      // Primary: By date (newest first)
        //            .ThenByDescending(p => p.CreatedDate)            // Secondary: By creation time
        //            .ThenByDescending(p => p.PrescriptionId)         // Tertiary: By ID for consistency
        //            .ToListAsync();

        //        var result = prescriptions.Select(p => new EmployeeDiagnosisListViewModel
        //        {
        //            PrescriptionId = p.PrescriptionId,
        //            EmployeeId = p.HrEmployee?.emp_id ?? "N/A",
        //            EmployeeName = p.HrEmployee?.emp_name ?? "N/A",
        //            Department = p.HrEmployee?.org_department?.dept_name ?? "N/A",
        //            Plant = p.OrgPlant?.plant_name ?? p.HrEmployee?.org_plant?.plant_name ?? "N/A",
        //            PrescriptionDate = p.PrescriptionDate,
        //            VisitType = ExtractVisitTypeFromRemarks(p.Remarks) ?? "Regular Visitor",
        //            ApprovalStatus = p.ApprovalStatus ?? "Completed",
        //            CreatedBy = p.CreatedBy ?? "N/A",
        //            DiseaseCount = p.PrescriptionDiseases?.Count ?? 0,
        //            MedicineCount = p.PrescriptionMedicines?.Count ?? 0,
        //            BloodPressure = SafeDecrypt(p.BloodPressure),
        //            Pulse = SafeDecrypt(p.Pulse),
        //            Temperature = SafeDecrypt(p.Temperature),
        //            ApprovedBy = p.ApprovedBy,
        //            ApprovedDate = p.ApprovedDate,
        //            RejectionReason = p.RejectionReason,
        //            Remarks = p.Remarks,
        //            PatientStatus = p.PatientStatus ?? "On Duty",
        //            // NEW: Extract dependent name from remarks or use database field if available
        //            DependentName = ExtractDependentNameFromRemarks(p.Remarks) ?? p.DependentName ?? "Self"
        //        }).ToList();

        //        Console.WriteLine($"✅ Found {result.Count} employee diagnoses for plant {userPlantId}");
        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Error getting all employee diagnoses: {ex.Message}");
        //        Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
        //        return new List<EmployeeDiagnosisListViewModel>();
        //    }
        //}

        // NEW: Helper method to extract dependent name from remarks
        private string? ExtractDependentNameFromRemarks(string? remarks)
        {
            if (string.IsNullOrEmpty(remarks))
                return "Self";

            try
            {
                // Look for pattern: "Patient: Dependent - DependentName"
                if (remarks.Contains("Patient: Dependent -", StringComparison.OrdinalIgnoreCase))
                {
                    var startIndex = remarks.IndexOf("Patient: Dependent -", StringComparison.OrdinalIgnoreCase);
                    if (startIndex >= 0)
                    {
                        var afterPrefix = remarks.Substring(startIndex + "Patient: Dependent -".Length).Trim();

                        // Find the end of the dependent name (either semicolon or end of string)
                        var endIndex = afterPrefix.IndexOf(';');
                        if (endIndex > 0)
                        {
                            return afterPrefix.Substring(0, endIndex).Trim();
                        }
                        else
                        {
                            return afterPrefix.Trim();
                        }
                    }
                }

                // Look for pattern: "Patient: Employee (Self)"
                if (remarks.Contains("Patient: Employee (Self)", StringComparison.OrdinalIgnoreCase))
                {
                    return "Self";
                }

                // Default to Self if no dependent pattern found
                return "Self";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting dependent name from remarks: {ex.Message}");
                return "Self";
            }
        }
        public async Task<bool> DeletePrescriptionAsync(int prescriptionId, int? userPlantId = null, string? deletedBy = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine($"🗑️ Deleting prescription ID: {prescriptionId} by: {deletedBy} in plant: {userPlantId}");

                // Find prescription with plant filtering
                var query = _db.MedPrescriptions
                    .Include(p => p.PrescriptionDiseases)
                    .Include(p => p.PrescriptionMedicines)
                    .Include(p => p.HrEmployee)
                    .Where(p => p.PrescriptionId == prescriptionId);

                // Apply plant filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                {
                    Console.WriteLine($"❌ Prescription {prescriptionId} not found or access denied for plant {userPlantId}");
                    return false;
                }

                Console.WriteLine($"✅ Found prescription for employee: {prescription.HrEmployee?.emp_name}, Plant: {prescription.PlantId}");

                // IMPORTANT: Restore medicine stock before deleting
                if (prescription.PrescriptionMedicines?.Any() == true)
                {
                    foreach (var prescriptionMedicine in prescription.PrescriptionMedicines)
                    {
                        // Find the indent item to restore stock
                        var indentBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == prescriptionMedicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == prescription.PlantId)
                            .OrderBy(b => b.ExpiryDate) // Use oldest batch first
                            .FirstOrDefaultAsync();

                        if (indentBatch != null)
                        {
                            var oldStock = indentBatch.AvailableStock;
                            indentBatch.AvailableStock += prescriptionMedicine.Quantity;

                            Console.WriteLine($"🔄 Restored stock for Medicine ID {prescriptionMedicine.MedItemId}: {oldStock} → {indentBatch.AvailableStock} (+{prescriptionMedicine.Quantity})");

                            _db.CompounderIndentBatches.Update(indentBatch);
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Could not find batch to restore stock for Medicine ID {prescriptionMedicine.MedItemId}");
                        }
                    }
                }

                // Delete prescription medicines first (foreign key constraint)
                if (prescription.PrescriptionMedicines?.Any() == true)
                {
                    _db.MedPrescriptionMedicines.RemoveRange(prescription.PrescriptionMedicines);
                    Console.WriteLine($"🗑️ Deleted {prescription.PrescriptionMedicines.Count} prescription medicines");
                }

                // Delete prescription diseases
                if (prescription.PrescriptionDiseases?.Any() == true)
                {
                    _db.MedPrescriptionDiseases.RemoveRange(prescription.PrescriptionDiseases);
                    Console.WriteLine($"🗑️ Deleted {prescription.PrescriptionDiseases.Count} prescription diseases");
                }

                // Delete the main prescription record
                _db.MedPrescriptions.Remove(prescription);
                Console.WriteLine($"🗑️ Deleted main prescription record");

                // Save all changes
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"✅ Prescription {prescriptionId} deleted successfully with stock restoration by {deletedBy} in plant {userPlantId}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error deleting prescription {prescriptionId}: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                throw; // Re-throw to let controller handle the error
            }
        }
        // Add this helper method to safely decrypt values
        private string SafeDecrypt(string? encryptedValue)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedValue))
                    return "";

                return _encryptionService.Decrypt(encryptedValue) ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Decryption error: {ex.Message}");
                return "***";
            }
        }

        // Add these methods to your DoctorDiagnosisRepository.cs class

        public async Task<PrescriptionEditPermissionResult> CanEditPrescriptionAsync(int prescriptionId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Checking edit permissions for prescription {prescriptionId} in plant {userPlantId}");

                var query = _db.MedPrescriptions
                    .Where(p => p.PrescriptionId == prescriptionId);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                {
                    Console.WriteLine($"❌ Prescription {prescriptionId} not found in plant {userPlantId}");
                    return new PrescriptionEditPermissionResult
                    {
                        CanEdit = false,
                        Message = "Prescription not found or you don't have access to it.",
                        PrescriptionExists = false,
                        IsInUserPlant = false
                    };
                }

                // Check if prescription can be edited (only Pending or Rejected)
                var canEdit = prescription.ApprovalStatus == "Pending" || prescription.ApprovalStatus == "Rejected";

                var result = new PrescriptionEditPermissionResult
                {
                    CanEdit = canEdit,
                    ApprovalStatus = prescription.ApprovalStatus ?? "Unknown",
                    PrescriptionExists = true,
                    IsInUserPlant = true,
                    Message = canEdit
                        ? "Prescription can be edited."
                        : $"Cannot edit prescription with status: {prescription.ApprovalStatus}. Only Pending or Rejected prescriptions can be edited."
                };

                Console.WriteLine($"✅ Edit permission check completed - CanEdit: {canEdit}, Status: {prescription.ApprovalStatus}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking edit permissions: {ex.Message}");
                return new PrescriptionEditPermissionResult
                {
                    CanEdit = false,
                    Message = "Error checking edit permissions.",
                    PrescriptionExists = false,
                    IsInUserPlant = false
                };
            }
        }

        public async Task<PrescriptionEditViewModel?> GetPrescriptionForEditAsync(int prescriptionId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Getting prescription {prescriptionId} for edit in plant {userPlantId}");

                var query = _db.MedPrescriptions
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_department)
                    .Include(p => p.HrEmployee)
                        .ThenInclude(e => e.org_plant)
                    .Include(p => p.PrescriptionDiseases)
                        .ThenInclude(pd => pd.MedDisease)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                        .ThenInclude(s => s.MedBase)
                    .Include(p => p.OrgPlant)
                    .Where(p => p.PrescriptionId == prescriptionId);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                {
                    Console.WriteLine($"❌ Prescription {prescriptionId} not found for edit in plant {userPlantId}");
                    return null;
                }

                // Check if can be edited
                var canEdit = prescription.ApprovalStatus == "Pending" || prescription.ApprovalStatus == "Rejected";
                if (!canEdit)
                {
                    Console.WriteLine($"❌ Prescription {prescriptionId} cannot be edited - Status: {prescription.ApprovalStatus}");
                    return null;
                }

                // Get available diseases for the plant
                var availableDiseases = await GetDiseasesAsync(userPlantId);

                // FIXED: Load current medicines with proper stock calculation
                var currentMedicines = new List<PrescriptionMedicineEdit>();
                if (prescription.PrescriptionMedicines != null && prescription.PrescriptionMedicines.Any())
                {
                    foreach (var pm in prescription.PrescriptionMedicines)
                    {
                        // Get the best matching batch for this medicine with current stock
                        var batchInfo = await (from batch in _db.CompounderIndentBatches
                                               join item in _db.CompounderIndentItems on batch.IndentItemId equals item.IndentItemId
                                               join indent in _db.CompounderIndents on item.IndentId equals indent.IndentId
                                               where item.MedItemId == pm.MedItemId
                                                     && batch.AvailableStock >= 0  // Include batches with 0 stock
                                                     && (userPlantId == null || indent.plant_id == userPlantId.Value)
                                               orderby batch.ExpiryDate, batch.BatchNo // FIFO
                                               select new
                                               {
                                                   batch.IndentItemId,
                                                   batch.BatchNo,
                                                   batch.ExpiryDate,
                                                   CurrentStock = batch.AvailableStock
                                               }).FirstOrDefaultAsync();

                        // Calculate editable stock: current DB stock + what was used in this prescription
                        int editableStock = (batchInfo?.CurrentStock ?? 0) + pm.Quantity;

                        Console.WriteLine($"💊 Medicine {pm.MedItemId} ({pm.MedMaster?.MedItemName}) - " +
                                        $"Current Stock: {batchInfo?.CurrentStock ?? 0}, " +
                                        $"Used in Prescription: {pm.Quantity}, " +
                                        $"Editable Stock: {editableStock}");

                        currentMedicines.Add(new PrescriptionMedicineEdit
                        {
                            PrescriptionMedicineId = pm.PrescriptionMedicineId,
                            MedItemId = pm.MedItemId,
                            MedicineName = pm.MedMaster?.MedItemName ?? "Unknown Medicine",
                            BaseName = pm.MedMaster?.MedBase?.BaseName ?? "Not Defined",
                            Quantity = pm.Quantity,
                            Dose = pm.Dose ?? "",
                            Instructions = pm.Instructions,
                            CompanyName = pm.MedMaster?.CompanyName,

                            // NEW: Include batch and stock information for proper edit handling
                            IndentItemId = batchInfo?.IndentItemId,
                            BatchNo = batchInfo?.BatchNo ?? "N/A",
                            ExpiryDate = batchInfo?.ExpiryDate,
                            AvailableStock = editableStock  // This is current stock + prescription quantity
                        });
                    }
                }

                // Build the edit view model
                var editModel = new PrescriptionEditViewModel
                {
                    PrescriptionId = prescription.PrescriptionId,
                    EmployeeId = prescription.HrEmployee?.emp_id ?? "N/A",
                    EmployeeName = prescription.HrEmployee?.emp_name ?? "N/A",
                    Department = prescription.HrEmployee?.org_department?.dept_name ?? "N/A",
                    Plant = prescription.OrgPlant?.plant_name ?? "N/A",
                    PrescriptionDate = prescription.PrescriptionDate,
                    CreatedBy = prescription.CreatedBy ?? "N/A",
                    ApprovalStatus = prescription.ApprovalStatus ?? "Unknown",
                    RejectionReason = prescription.RejectionReason,

                    // Decrypt vital signs
                    BloodPressure = SafeDecrypt(prescription.BloodPressure),
                    Pulse = SafeDecrypt(prescription.Pulse),
                    Temperature = SafeDecrypt(prescription.Temperature),

                    PatientStatus = prescription.PatientStatus ?? "On Duty",
                    VisitType = ExtractVisitTypeFromRemarks(prescription.Remarks) ?? "Regular Visitor",
                    Remarks = prescription.Remarks,

                    // Selected diseases
                    SelectedDiseaseIds = prescription.PrescriptionDiseases?.Select(pd => pd.DiseaseId).ToList() ?? new List<int>(),
                    AvailableDiseases = availableDiseases,

                    // Current medicines with proper stock calculation
                    CurrentMedicines = currentMedicines,

                    Employee = prescription.HrEmployee
                };

                Console.WriteLine($"✅ Prescription edit data loaded - Diseases: {editModel.SelectedDiseaseIds.Count}, Medicines: {editModel.CurrentMedicines.Count}");
                return editModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting prescription for edit: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<PrescriptionUpdateResult> UpdatePrescriptionAsync(int prescriptionId,
        List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
        VitalSigns vitalSigns, string modifiedBy, int? userPlantId = null,
        string? visitType = null, string? patientStatus = null, string? dependentName = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var selectedDependentName = dependentName ?? "Self";

                Console.WriteLine($"🔄 Updating prescription {prescriptionId} by {modifiedBy} in plant {userPlantId}");

                // Get existing prescription with all related data
                var query = _db.MedPrescriptions
                    .Include(p => p.PrescriptionDiseases)
                    .Include(p => p.PrescriptionMedicines)
                        .ThenInclude(pm => pm.MedMaster)
                    .Include(p => p.HrEmployee)
                    .Where(p => p.PrescriptionId == prescriptionId);

                if (userPlantId.HasValue)
                {
                    query = query.Where(p => p.PlantId == userPlantId.Value);
                }

                var prescription = await query.FirstOrDefaultAsync();

                if (prescription == null)
                {
                    return new PrescriptionUpdateResult
                    {
                        Success = false,
                        Message = "Prescription not found or access denied."
                    };
                }

                // Verify can edit
                if (prescription.ApprovalStatus != "Pending" && prescription.ApprovalStatus != "Rejected")
                {
                    return new PrescriptionUpdateResult
                    {
                        Success = false,
                        Message = $"Cannot edit prescription with status: {prescription.ApprovalStatus}"
                    };
                }

                // NEW: Get dependent details if updating with dependent name
                HrEmployeeDependent? dependentDetails = null;
                if (selectedDependentName != "Self")
                {
                    dependentDetails = await _db.HrEmployeeDependents
                        .FirstOrDefaultAsync(d => d.emp_uid == prescription.HrEmployee.emp_uid &&
                                                  d.dep_name == selectedDependentName &&
                                                  d.is_active);

                    if (dependentDetails == null)
                    {
                        return new PrescriptionUpdateResult
                        {
                            Success = false,
                            Message = $"Dependent '{selectedDependentName}' not found for this employee."
                        };
                    }
                }

                var validationErrors = new List<string>();

                // Validate diseases
                if (selectedDiseases?.Any() != true)
                {
                    validationErrors.Add("At least one disease must be selected.");
                }

                // For medicines, resolve IndentItemId for any that don't have it
                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines.Where(m => !m.IndentItemId.HasValue || m.IndentItemId.Value == 0))
                    {
                        // Find the best batch for this medicine
                        var bestBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == medicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == prescription.PlantId)
                            .Where(b => b.AvailableStock > 0)
                            .OrderBy(b => b.ExpiryDate)
                            .ThenBy(b => b.BatchNo)
                            .FirstOrDefaultAsync();

                        if (bestBatch != null)
                        {
                            medicine.IndentItemId = bestBatch.IndentItemId;
                            medicine.BatchNo = bestBatch.BatchNo;
                            medicine.ExpiryDate = bestBatch.ExpiryDate;
                            medicine.AvailableStock = bestBatch.AvailableStock;
                        }
                    }

                    // Validate medicine stock
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);

                            // For existing medicines, add back current quantity if not rejected
                            var existingMedicine = prescription.PrescriptionMedicines?
                                .FirstOrDefault(pm => pm.MedItemId == medicine.MedItemId);

                            var adjustedStock = availableStock;
                            if (existingMedicine != null && prescription.ApprovalStatus != "Rejected")
                            {
                                adjustedStock += existingMedicine.Quantity;
                            }

                            if (medicine.Quantity > adjustedStock)
                            {
                                validationErrors.Add($"Insufficient stock for {medicine.MedicineName}. Available: {adjustedStock}, Requested: {medicine.Quantity}");
                            }
                        }
                    }
                }

                if (validationErrors.Any())
                {
                    return new PrescriptionUpdateResult
                    {
                        Success = false,
                        Message = "Validation failed.",
                        ValidationErrors = validationErrors
                    };
                }

                // Step 1: Restore stock from all existing medicines (only if not rejected)
                // Simplified bulk restoration approach - matches OthersDiagnosis logic
                if (prescription.PrescriptionMedicines?.Any() == true && prescription.ApprovalStatus != "Rejected")
                {
                    foreach (var existingMedicine in prescription.PrescriptionMedicines)
                    {
                        // Find the batch to restore stock
                        var indentBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == existingMedicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == prescription.PlantId)
                            .OrderBy(b => b.ExpiryDate)
                            .ThenBy(b => b.BatchNo)
                            .FirstOrDefaultAsync();

                        if (indentBatch != null)
                        {
                            indentBatch.AvailableStock += existingMedicine.Quantity;
                            Console.WriteLine($"🔄 Restored {existingMedicine.Quantity} units to batch for Doctor medicine {existingMedicine.MedItemId}");
                        }
                    }
                }


                // Step 2: Update prescription basic info
                prescription.BloodPressure = _encryptionService.Encrypt(vitalSigns.BloodPressure ?? "");
                prescription.Pulse = _encryptionService.Encrypt(vitalSigns.Pulse ?? "");
                prescription.Temperature = _encryptionService.Encrypt(vitalSigns.Temperature ?? "");
                prescription.PatientStatus = patientStatus ?? "On Duty";

                // Update remarks with visit type, patient status AND dependent info
                var remarks = new List<string>();
                if (!string.IsNullOrEmpty(visitType))
                {
                    remarks.Add($"Visit Type: {visitType}");
                }
                if (!string.IsNullOrEmpty(patientStatus))
                {
                    remarks.Add($"Patient Status: {patientStatus}");
                }

                if (!string.IsNullOrEmpty(selectedDependentName) && selectedDependentName != "Self")
                {
                    if (dependentDetails != null)
                    {
                        remarks.Add($"Patient: Dependent - {selectedDependentName}; Relation: {dependentDetails.relation ?? "Not Specified"}");
                    }
                    else
                    {
                        remarks.Add($"Patient: Dependent - {selectedDependentName}; Relation: Not Specified");
                    }
                }
                else
                {
                    remarks.Add($"Patient: Employee (Self)");
                }
                prescription.Remarks = string.Join("; ", remarks);

                prescription.DependentName = selectedDependentName != "Self" ? selectedDependentName : null;

                // Reset approval status to Pending
                prescription.ApprovalStatus = "Pending";
                prescription.ApprovedBy = null;
                prescription.ApprovedDate = null;
                prescription.RejectionReason = null;

                // Step 3: Remove existing diseases and medicines
                if (prescription.PrescriptionDiseases?.Any() == true)
                {
                    _db.MedPrescriptionDiseases.RemoveRange(prescription.PrescriptionDiseases);
                }

                if (prescription.PrescriptionMedicines?.Any() == true)
                {
                    _db.MedPrescriptionMedicines.RemoveRange(prescription.PrescriptionMedicines);
                }

                // Step 4: Add new diseases
                if (selectedDiseases?.Any() == true)
                {
                    var prescriptionDiseases = selectedDiseases.Select(diseaseId => new MedPrescriptionDisease
                    {
                        PrescriptionId = prescriptionId,
                        DiseaseId = diseaseId,
                        CreatedBy = modifiedBy,
                        CreatedDate = DateTime.Now
                    }).ToList();

                    _db.MedPrescriptionDiseases.AddRange(prescriptionDiseases);
                }

                // Step 5: Add new medicines and adjust stock
                var affectedMedicines = 0;
                if (medicines?.Any() == true)
                {
                    var prescriptionMedicines = medicines.Select(med => new MedPrescriptionMedicine
                    {
                        PrescriptionId = prescriptionId,
                        MedItemId = med.MedItemId,
                        Quantity = med.Quantity,
                        Dose = med.Dose ?? "",
                        Instructions = $"{_encryptionService.Encrypt($"ID:{med.MedItemId} - {med.MedicineName}")} - {med.Dose}"
                    }).ToList();

                    _db.MedPrescriptionMedicines.AddRange(prescriptionMedicines);

                    // Adjust stock for new medicine quantities
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var stockUpdated = await UpdateAvailableStockAsync(medicine.IndentItemId.Value, medicine.Quantity, userPlantId);
                            if (stockUpdated)
                            {
                                affectedMedicines++;
                            }
                        }
                    }
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                var patientDisplay = selectedDependentName != "Self" ?
                    $"Dependent: {selectedDependentName} ({dependentDetails?.relation ?? "Unknown Relation"})" :
                    "Employee (Self)";
                Console.WriteLine($"✅ Prescription {prescriptionId} updated successfully for {patientDisplay} - Diseases: {selectedDiseases?.Count ?? 0}, Medicines: {affectedMedicines}");

                return new PrescriptionUpdateResult
                {
                    Success = true,
                    Message = $"Prescription updated successfully for {patientDisplay}. Status reset to Pending for approval.",
                    StockAdjusted = true,
                    AffectedMedicines = affectedMedicines
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error updating prescription {prescriptionId}: {ex.Message}");

                return new PrescriptionUpdateResult
                {
                    Success = false,
                    Message = "Error updating prescription: " + ex.Message
                };
            }
        }

    }
}