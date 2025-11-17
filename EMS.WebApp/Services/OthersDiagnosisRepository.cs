using EMS.WebApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class OthersDiagnosisRepository : IOthersDiagnosisRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly IEncryptionService _encryptionService;

        public OthersDiagnosisRepository(ApplicationDbContext db, IEncryptionService encryptionService)
        {
            _db = db;
            _encryptionService = encryptionService;
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

        // ======= NEW: Get medicines with batch information and stock, sorted by expiry date WITH PLANT FILTERING =======
        public async Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Getting medicines from compounder indent with FIFO logic for Others Diagnosis (Plant: {userPlantId})");

                var query = _db.CompounderIndentItems
                    .Include(i => i.MedMaster)
                        .ThenInclude(m => m.MedBase)
                    .Include(i => i.CompounderIndent)
                    .AsQueryable();

                // Plant-wise filtering for compounder indents
                if (userPlantId.HasValue)
                {
                    query = query.Where(i => i.CompounderIndent.plant_id == userPlantId.Value);
                }

                var medicineStocks = await query
                    .SelectMany(i => _db.CompounderIndentBatches
                        .Where(b => b.IndentItemId == i.IndentItemId &&
                                   i.ReceivedQuantity > 0 &&
                                   b.AvailableStock > 0 &&
                                   !string.IsNullOrEmpty(b.BatchNo) &&
                                   b.ExpiryDate >= DateTime.Today)
                        .Select(b => new MedicineStockInfo
                        {
                            IndentItemId = i.IndentItemId, // FIXED: Keep actual IndentItemId for each batch
                            MedItemId = i.MedItemId,
                            MedItemName = i.MedMaster.MedItemName,
                            CompanyName = i.MedMaster.CompanyName ?? "Not Defined",
                            BatchNo = b.BatchNo,
                            ExpiryDate = b.ExpiryDate,
                            AvailableStock = b.AvailableStock, // FIXED: Actual stock for this specific batch
                            BaseName = i.MedMaster.MedBase != null
                                ? i.MedMaster.MedBase.BaseName
                                : "Not Defined",
                            PlantId = i.CompounderIndent.plant_id
                        }))
                    .OrderBy(m => m.ExpiryDate) // Order by medicine name
                    .ThenBy(m => m.BatchNo) // Then by batch
                    //.ThenBy(m => m.ExpiryDate ?? DateTime.MaxValue) // Then by expiry (FIFO)
                    .ToListAsync();

                Console.WriteLine($"✅ Found {medicineStocks.Count} individual medicine batches with available stock for Others Diagnosis (Plant: {userPlantId})");

                return medicineStocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting medicines from compounder indent for Others Diagnosis: {ex.Message}");

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

        // ======= NEW: Get available stock for a specific medicine batch WITH PLANT FILTERING =======
        public async Task<int> GetAvailableStockAsync(int indentItemId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Getting available stock for IndentItemId {indentItemId} using FIFO logic for Others Diagnosis (Plant: {userPlantId})");

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
                    Console.WriteLine($"✅ FIFO Stock found for Others Diagnosis IndentItemId {indentItemId}: {result.AvailableStock} units (Batch: {result.BatchNo}, Expiry: {result.ExpiryDate.ToString("dd/MM/yyyy") ?? "N/A"})");
                    return result.AvailableStock;
                }

                Console.WriteLine($"⚠️ No available stock found for Others Diagnosis IndentItemId {indentItemId} in plant {userPlantId}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting available stock for Others Diagnosis item {indentItemId}: {ex.Message}");
                return 0;
            }
        }
        // ======= NEW: Update available stock after prescription WITH PLANT FILTERING =======
        public async Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔄 Updating available stock for Others Diagnosis IndentItemId {indentItemId}, using {quantityUsed} units with FIFO logic (Plant: {userPlantId})");

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
                    Console.WriteLine($"❌ Others Diagnosis compounder indent item {indentItemId} not found or no available stock for plant {userPlantId}");
                    return false;
                }

                var batch = result.Batch;
                if (batch.AvailableStock < quantityUsed)
                {
                    Console.WriteLine($"❌ Insufficient stock in Others Diagnosis FIFO batch. Available: {batch.AvailableStock}, Requested: {quantityUsed} (Batch: {result.BatchNo})");
                    return false;
                }

                var oldStock = batch.AvailableStock;
                batch.AvailableStock = oldStock - quantityUsed;

                _db.CompounderIndentBatches.Update(batch);
                await _db.SaveChangesAsync();

                Console.WriteLine($"✅ Others Diagnosis FIFO Stock updated for IndentItemId {indentItemId}: {oldStock} → {batch.AvailableStock} (Batch: {result.BatchNo}, Expiry: {result.ExpiryDate.ToString("dd/MM/yyyy") ?? "N/A"}, Plant: {userPlantId})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating available stock for Others Diagnosis item {indentItemId}: {ex.Message}");
                return false;
            }
        }

        // ======= UPDATED: SaveDiagnosisAsync with BCM plant approval logic AND PLANT FILTERING =======
        public async Task<(bool Success, string ErrorMessage)> SaveDiagnosisAsync(OthersDiagnosisViewModel model, string createdBy, int? userPlantId = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (!userPlantId.HasValue)
                {
                    return (false, "User is not assigned to any plant. Please contact administrator.");
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(model.PatientName))
                    return (false, "Patient name is required");

                if (model.Age.HasValue && (model.Age.Value < 0 || model.Age.Value > 120))
                    return (false, "Age must be between 0 and 120 years");

                if (string.IsNullOrWhiteSpace(model.DiagnosedBy))
                    return (false, "Diagnosed By is required");

                // Auto-generate TreatmentId if not provided
                if (string.IsNullOrWhiteSpace(model.TreatmentId))
                {
                    model.TreatmentId = await GenerateNewTreatmentIdAsync();
                }

                if (string.IsNullOrWhiteSpace(model.VisitType))
                    model.VisitType = "Regular Visitor";

                // Validate medicine stock before saving
                if (model.PrescriptionMedicines?.Any() == true)
                {
                    foreach (var medicine in model.PrescriptionMedicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);
                            if (availableStock < medicine.Quantity)
                            {
                                return (false, $"Insufficient stock for {medicine.MedicineName}. Available: {availableStock}, Requested: {medicine.Quantity}");
                            }
                        }
                    }
                }

                // Handle patient creation/update
                OtherPatient? patient;

                if (model.PatientId.HasValue)
                {
                    var patientQuery = _db.OtherPatients.AsQueryable();
                    if (userPlantId.HasValue)
                    {
                        patientQuery = patientQuery.Where(p => _db.OthersDiagnoses
                            .Any(d => d.PatientId == p.PatientId && d.PlantId == userPlantId.Value));
                    }

                    patient = await patientQuery.FirstOrDefaultAsync(p => p.PatientId == model.PatientId.Value);
                    if (patient == null)
                        return (false, $"Patient with ID {model.PatientId} not found or access denied");

                    patient.PatientName = model.PatientName;
                    patient.Age = model.Age ?? 0;
                    patient.PNumber = model.PNumber ?? string.Empty;
                    patient.Category = model.Category ?? string.Empty;
                    patient.OtherDetails = model.OtherDetails;
                }
                else
                {
                    var existingQuery = _db.OtherPatients
                        .Where(p => p.TreatmentId == model.TreatmentId);

                    if (userPlantId.HasValue)
                    {
                        existingQuery = existingQuery.Where(p => _db.OthersDiagnoses
                            .Any(d => d.PatientId == p.PatientId && d.PlantId == userPlantId.Value));
                    }

                    patient = await existingQuery.FirstOrDefaultAsync();

                    if (patient != null)
                    {
                        patient.PatientName = model.PatientName;
                        patient.Age = model.Age ?? 0;
                        patient.PNumber = model.PNumber ?? string.Empty;
                        patient.Category = model.Category ?? string.Empty;
                        patient.OtherDetails = model.OtherDetails;
                    }
                    else
                    {
                        patient = new OtherPatient
                        {
                            TreatmentId = model.TreatmentId,
                            PatientName = model.PatientName,
                            Age = model.Age ?? 0,
                            PNumber = model.PNumber ?? string.Empty,
                            Category = model.Category ?? string.Empty,
                            OtherDetails = model.OtherDetails,
                            CreatedBy = createdBy,
                            CreatedDate = DateTime.Now
                        };
                        _db.OtherPatients.Add(patient);
                    }
                }

                // Save patient first to get PatientId
                await _db.SaveChangesAsync();

                // Enhanced approval logic
                var plantCode = await GetPlantCodeAsync(userPlantId.Value);
                string approvalStatus;
                string? approvedBy = null;
                DateTime? approvedDate = null;

                if (plantCode?.ToUpper() == "BCM" || model.VisitType == "First Aid or Emergency")
                {
                    approvalStatus = "Pending";
                }
                else
                {
                    approvalStatus = "Approved";
                    approvedBy = createdBy;
                    approvedDate = DateTime.Now;
                }

                // Create diagnosis record
                var diagnosis = new OthersDiagnosis
                {
                    PatientId = patient.PatientId, // Use the generated/existing PatientId
                    PlantId = (short)userPlantId.Value,
                    VisitDate = DateTime.Now,
                    LastVisitDate = model.LastVisitDate,
                    BloodPressure = _encryptionService.Encrypt(model.BloodPressure),
                    PulseRate = _encryptionService.Encrypt(model.PulseRate),
                    Sugar = _encryptionService.Encrypt(model.Sugar),
                    Remarks = model.Remarks,
                    DiagnosedBy = model.DiagnosedBy,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now,
                    VisitType = model.VisitType,
                    ApprovalStatus = approvalStatus,
                    ApprovedBy = approvedBy,
                    ApprovedDate = approvedDate
                };

                _db.OthersDiagnoses.Add(diagnosis);
                await _db.SaveChangesAsync(); // Save to get DiagnosisId

                // FIXED: Save diagnosed diseases using the generated DiagnosisId
                if (model.SelectedDiseaseIds?.Any() == true)
                {
                    var diagnosisDiseases = new List<OthersDiagnosisDisease>();
                    foreach (var diseaseId in model.SelectedDiseaseIds)
                    {
                        diagnosisDiseases.Add(new OthersDiagnosisDisease
                        {
                            DiagnosisId = diagnosis.DiagnosisId, // Use the generated ID
                            DiseaseId = diseaseId
                        });
                    }

                    _db.OthersDiagnosisDiseases.AddRange(diagnosisDiseases);
                    Console.WriteLine($"✅ Prepared {diagnosisDiseases.Count} diseases for diagnosis {diagnosis.DiagnosisId}");
                }

                // FIXED: Save prescribed medicines using the generated DiagnosisId
                if (model.PrescriptionMedicines?.Any() == true)
                {
                    // Validate medicine IDs exist
                    var medItemIds = model.PrescriptionMedicines.Select(m => m.MedItemId).ToList();
                    var existingMedicines = await _db.med_masters
                        .Where(m => medItemIds.Contains(m.MedItemId))
                        .Select(m => new { m.MedItemId, m.MedItemName })
                        .ToListAsync();

                    var existingMedItemIds = existingMedicines.Select(m => m.MedItemId).ToList();
                    var missingMedicines = medItemIds.Except(existingMedItemIds).ToList();

                    if (missingMedicines.Any())
                    {
                        return (false, $"Invalid medicine IDs: {string.Join(", ", missingMedicines)}");
                    }

                    var diagnosisMedicines = new List<OthersDiagnosisMedicine>();

                    // Group medicines by MedItemId to handle multiple batches
                    var medicineGroups = model.PrescriptionMedicines.GroupBy(m => m.MedItemId).ToList();

                    foreach (var group in medicineGroups)
                    {
                        foreach (var med in group)
                        {
                            var encryptedMedicineName = _encryptionService.Encrypt(med.MedicineName);
                            var instructions = !string.IsNullOrEmpty(med.Instructions)
                                ? $"{encryptedMedicineName} - {med.Instructions}"
                                : $"{encryptedMedicineName} - {med.Dose}";

                            diagnosisMedicines.Add(new OthersDiagnosisMedicine
                            {
                                DiagnosisId = diagnosis.DiagnosisId, // Use the generated ID
                                MedItemId = med.MedItemId,
                                Quantity = med.Quantity,
                                Dose = med.Dose ?? string.Empty,
                                Instructions = instructions
                            });
                        }
                    }

                    _db.OthersDiagnosisMedicines.AddRange(diagnosisMedicines);

                    // Update available stock for each medicine batch
                    foreach (var medicine in model.PrescriptionMedicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var stockUpdated = await UpdateAvailableStockAsync(medicine.IndentItemId.Value, medicine.Quantity, userPlantId);
                            if (!stockUpdated)
                            {
                                return (false, $"Failed to update stock for {medicine.MedicineName}");
                            }
                        }
                    }

                    Console.WriteLine($"✅ Prepared {diagnosisMedicines.Count} medicines for diagnosis {diagnosis.DiagnosisId}");
                }

                // Save all child records together
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Return success message based on approval status
                string successMessage;
                if (approvalStatus == "Pending")
                {
                    if (plantCode?.ToUpper() == "BCM")
                    {
                        successMessage = $"Diagnosis saved with Treatment ID: {model.TreatmentId} and sent for doctor approval (BCM Plant - All visits require approval).";
                    }
                    else
                    {
                        successMessage = $"Emergency diagnosis saved with Treatment ID: {model.TreatmentId} and sent for doctor approval.";
                    }
                }
                else
                {
                    successMessage = $"Diagnosis saved successfully with Treatment ID: {model.TreatmentId}.";
                }

                return (true, successMessage);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Unexpected error in SaveDiagnosisAsync: {ex.Message}");
                return (false, $"Unexpected error: {ex.Message}");
            }
        }
        // ======= UPDATED: GetAllDiagnosesAsync WITH PLANT FILTERING =======
        public async Task<List<OthersDiagnosisListViewModel>> GetAllDiagnosesAsync(int? userPlantId = null)
        {
            var query = _db.OthersDiagnoses
                .Include(d => d.Patient)
                .Include(d => d.OrgPlant) // NEW: Include plant info
                .AsQueryable();

            // NEW: Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.PlantId == userPlantId.Value);
            }

            return await query
                .OrderByDescending(d => d.VisitDate)
                .ThenByDescending(d => d.CreatedDate)
                .ThenByDescending(d => d.DiagnosisId)
                .Select(d => new OthersDiagnosisListViewModel
                {
                    DiagnosisId = d.DiagnosisId,
                    TreatmentId = d.Patient!.TreatmentId,
                    PatientName = d.Patient.PatientName,
                    Age = d.Patient.Age == 0 ? null : d.Patient.Age,
                    Category = d.Patient.Category,
                    VisitDate = d.VisitDate,
                    DiagnosedBy = d.DiagnosedBy,
                    VisitType = d.VisitType,
                    ApprovalStatus = d.ApprovalStatus,
                    PlantName = d.OrgPlant != null ? d.OrgPlant.plant_name : "Unknown Plant" // NEW: Plant name
                })
                .ToListAsync();
        }

        // ======= KEEP ALL EXISTING METHODS BUT ADD PLANT FILTERING =======

        public async Task<string> GenerateNewTreatmentIdAsync()
        {
            try
            {
                Console.WriteLine("🔢 Generating new Treatment ID...");

                // Get all TreatmentIds that start with "T" - bring to memory first to avoid EF translation issues
                var treatmentIds = await _db.OtherPatients
                    .Where(p => p.TreatmentId.StartsWith("T") && p.TreatmentId.Length > 1)
                    .Select(p => p.TreatmentId)
                    .ToListAsync();

                Console.WriteLine($"Found {treatmentIds.Count} existing Treatment IDs starting with 'T'");

                if (!treatmentIds.Any())
                {
                    Console.WriteLine("No existing Treatment IDs found. Returning T1");
                    return "T1";
                }

                // Process in memory to find the highest number
                int maxNumber = 0;
                var validIds = new List<string>();

                foreach (var id in treatmentIds)
                {
                    if (id.Length > 1)
                    {
                        var numberPart = id.Substring(1);
                        if (int.TryParse(numberPart, out int number))
                        {
                            validIds.Add(id);
                            if (number > maxNumber)
                            {
                                maxNumber = number;
                            }
                        }
                    }
                }

                var newId = $"T{maxNumber + 1}";
                Console.WriteLine($"✅ Generated new Treatment ID: {newId} (previous highest: T{maxNumber})");
                Console.WriteLine($"Valid existing IDs: [{string.Join(", ", validIds.Take(10))}]");

                return newId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating Treatment ID: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Using fallback Treatment ID: T1");
                return "T1";
            }
        }

        // NEW: GetPatientByTreatmentIdAsync WITH PLANT FILTERING
        public async Task<OtherPatient?> GetPatientByTreatmentIdAsync(string treatmentId, int? userPlantId = null)
        {
            var query = _db.OtherPatients
                .Include(p => p.Diagnoses.OrderByDescending(d => d.VisitDate).Take(1))
                .AsQueryable();

            // NEW: Plant filtering for patient lookup
            if (userPlantId.HasValue)
            {
                query = query.Where(p => p.Diagnoses.Any(d => d.PlantId == userPlantId.Value));
            }

            return await query.FirstOrDefaultAsync(p => p.TreatmentId == treatmentId);
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

        // NEW: GetDiagnosisDetailsAsync WITH PLANT FILTERING
        public async Task<OthersDiagnosisDetailsViewModel?> GetDiagnosisDetailsAsync(int diagnosisId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"=== GetDiagnosisDetailsAsync for ID: {diagnosisId} ===");

                // Get the basic diagnosis info first WITH PLANT FILTERING
                var diagnosisQuery = _db.OthersDiagnoses
                    .Include(d => d.Patient)
                    .Include(d => d.OrgPlant) // NEW: Include plant info
                    .AsQueryable();

                // NEW: Plant filtering
                if (userPlantId.HasValue)
                {
                    diagnosisQuery = diagnosisQuery.Where(d => d.PlantId == userPlantId.Value);
                }

                var diagnosis = await diagnosisQuery.FirstOrDefaultAsync(d => d.DiagnosisId == diagnosisId);

                if (diagnosis == null)
                {
                    Console.WriteLine("Diagnosis not found or access denied");
                    return null;
                }

                Console.WriteLine($"Found diagnosis for patient: {diagnosis.Patient?.PatientName}");

                // Get diseases using explicit join
                var diseases = await (from dd in _db.OthersDiagnosisDiseases
                                      join md in _db.MedDiseases on dd.DiseaseId equals md.DiseaseId
                                      where dd.DiagnosisId == diagnosisId
                                      select new OthersDiseaseDetails
                                      {
                                          DiseaseId = dd.DiseaseId,
                                          DiseaseName = md.DiseaseName,
                                          DiseaseDescription = md.DiseaseDesc
                                      }).ToListAsync();

                Console.WriteLine($"Found {diseases.Count} diseases");

                // Get medicines with DECRYPTION
                var medicines = await (from dm in _db.OthersDiagnosisMedicines
                                       join mm in _db.med_masters on dm.MedItemId equals mm.MedItemId
                                       where dm.DiagnosisId == diagnosisId
                                       select new
                                       {
                                           dm.MedItemId,
                                           mm.MedItemName,
                                           dm.Quantity,
                                           dm.Dose,
                                           dm.Instructions,
                                           mm.CompanyName
                                       }).ToListAsync();

                // Decrypt medicine names from instructions
                var decryptedMedicines = medicines.Select(m => {
                    var medicineName = m.MedItemName;

                    if (!string.IsNullOrEmpty(m.Instructions) && m.Instructions.Contains(" - "))
                    {
                        var parts = m.Instructions.Split(" - ", 2);
                        if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                        {
                            var decryptedName = _encryptionService.Decrypt(parts[0]);
                            if (!string.IsNullOrEmpty(decryptedName))
                            {
                                medicineName = decryptedName;
                            }
                        }
                    }

                    return new OthersMedicineDetails
                    {
                        MedItemId = m.MedItemId,
                        MedicineName = medicineName,
                        Quantity = m.Quantity,
                        Dose = m.Dose,
                        Instructions = m.Instructions,
                        CompanyName = m.CompanyName
                    };
                }).ToList();

                Console.WriteLine($"Found {decryptedMedicines.Count} medicines using join query");

                var result = new OthersDiagnosisDetailsViewModel
                {
                    DiagnosisId = diagnosis.DiagnosisId,
                    TreatmentId = diagnosis.Patient?.TreatmentId ?? "N/A",
                    PatientName = diagnosis.Patient?.PatientName ?? "N/A",
                    Age = diagnosis.Patient?.Age == 0 ? null : diagnosis.Patient?.Age,
                    PNumber = diagnosis.Patient?.PNumber ?? "N/A",
                    Category = diagnosis.Patient?.Category ?? "N/A",
                    OtherDetails = diagnosis.Patient?.OtherDetails,
                    VisitDate = diagnosis.VisitDate,
                    LastVisitDate = diagnosis.LastVisitDate,
                    // DECRYPT vital signs
                    BloodPressure = _encryptionService.Decrypt(diagnosis.BloodPressure),
                    PulseRate = _encryptionService.Decrypt(diagnosis.PulseRate),
                    Sugar = _encryptionService.Decrypt(diagnosis.Sugar),
                    Remarks = diagnosis.Remarks,
                    DiagnosedBy = diagnosis.DiagnosedBy,
                    VisitType = diagnosis.VisitType,
                    ApprovalStatus = diagnosis.ApprovalStatus,
                    ApprovedBy = diagnosis.ApprovedBy,
                    ApprovedDate = diagnosis.ApprovedDate,
                    RejectionReason = diagnosis.RejectionReason,
                    PlantName = diagnosis.OrgPlant?.plant_name ?? "Unknown Plant", // NEW: Plant name
                    Diseases = diseases,
                    Medicines = decryptedMedicines
                };

                Console.WriteLine($"Returning result with {result.Diseases.Count} diseases and {result.Medicines.Count} medicines");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetDiagnosisDetailsAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // NEW: DeleteDiagnosisAsync WITH PLANT FILTERING
        public async Task<bool> DeleteDiagnosisAsync(int diagnosisId, int? userPlantId = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine($"Deleting Others diagnosis ID: {diagnosisId} in plant: {userPlantId}");

                var query = _db.OthersDiagnoses
                    .Include(d => d.DiagnosisDiseases)
                    .Include(d => d.DiagnosisMedicines) // Include medicines to restore stock
                    .Include(d => d.Patient)
                    .AsQueryable();

                // Plant filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(d => d.PlantId == userPlantId.Value);
                }

                var diagnosis = await query.FirstOrDefaultAsync(d => d.DiagnosisId == diagnosisId);

                if (diagnosis == null)
                {
                    Console.WriteLine($"Others diagnosis {diagnosisId} not found or access denied for plant {userPlantId}");
                    return false;
                }

                Console.WriteLine($"Found Others diagnosis for patient: {diagnosis.Patient?.PatientName}, Plant: {diagnosis.PlantId}");

                // IMPORTANT: Restore medicine stock before deleting using consistent FIFO logic
                if (diagnosis.DiagnosisMedicines?.Any() == true)
                {
                    Console.WriteLine($"Restoring stock for {diagnosis.DiagnosisMedicines.Count} medicines due to Others diagnosis deletion");

                    foreach (var diagnosisMedicine in diagnosis.DiagnosisMedicines)
                    {
                        // Use consistent FIFO logic with UpdateAvailableStockAsync
                        var indentBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == diagnosisMedicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == diagnosis.PlantId)
                            .OrderBy(b => b.ExpiryDate) // Handle null expiry dates
                            .ThenBy(b => b.BatchNo) // Secondary sort for consistency
                            .FirstOrDefaultAsync();

                        if (indentBatch != null)
                        {
                            var oldStock = indentBatch.AvailableStock;
                            indentBatch.AvailableStock += diagnosisMedicine.Quantity;

                            Console.WriteLine($"Restored stock for Medicine ID {diagnosisMedicine.MedItemId}: {oldStock} -> {indentBatch.AvailableStock} (+{diagnosisMedicine.Quantity}) (Batch: {indentBatch.BatchNo})");

                            _db.CompounderIndentBatches.Update(indentBatch);
                        }
                        else
                        {
                            Console.WriteLine($"Could not find batch to restore stock for Medicine ID {diagnosisMedicine.MedItemId}");

                            // Try to find any batch for this medicine as fallback
                            var fallbackBatch = await _db.CompounderIndentBatches
                                .Include(b => b.CompounderIndentItem)
                                    .ThenInclude(i => i.CompounderIndent)
                                .Where(b => b.CompounderIndentItem.MedItemId == diagnosisMedicine.MedItemId)
                                .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == diagnosis.PlantId)
                                .FirstOrDefaultAsync();

                            if (fallbackBatch != null)
                            {
                                var oldStock = fallbackBatch.AvailableStock;
                                fallbackBatch.AvailableStock += diagnosisMedicine.Quantity;

                                Console.WriteLine($"FALLBACK: Restored stock for Medicine ID {diagnosisMedicine.MedItemId}: {oldStock} -> {fallbackBatch.AvailableStock} (+{diagnosisMedicine.Quantity}) to batch {fallbackBatch.BatchNo}");

                                _db.CompounderIndentBatches.Update(fallbackBatch);
                            }
                            else
                            {
                                Console.WriteLine($"CRITICAL: No batch found to restore stock for Medicine ID {diagnosisMedicine.MedItemId}. Stock may be inconsistent!");
                            }
                        }
                    }
                }

                // Delete diagnosis medicines first (foreign key constraint)
                if (diagnosis.DiagnosisMedicines?.Any() == true)
                {
                    _db.OthersDiagnosisMedicines.RemoveRange(diagnosis.DiagnosisMedicines);
                    Console.WriteLine($"Deleted {diagnosis.DiagnosisMedicines.Count} diagnosis medicines");
                }

                // Delete diagnosis diseases
                if (diagnosis.DiagnosisDiseases?.Any() == true)
                {
                    _db.OthersDiagnosisDiseases.RemoveRange(diagnosis.DiagnosisDiseases);
                    Console.WriteLine($"Deleted {diagnosis.DiagnosisDiseases.Count} diagnosis diseases");
                }

                // Delete the main diagnosis record
                _db.OthersDiagnoses.Remove(diagnosis);
                Console.WriteLine("Deleted main diagnosis record");

                // Save all changes
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"Others diagnosis {diagnosisId} deleted successfully with stock restoration in plant {userPlantId}");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error deleting Others diagnosis {diagnosisId}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to let controller handle the error
            }
        }
        // NEW: GetPatientForEditAsync WITH PLANT FILTERING
        public async Task<OthersDiagnosisViewModel?> GetPatientForEditAsync(string treatmentId, int? userPlantId = null)
        {
            var patient = await GetPatientByTreatmentIdAsync(treatmentId, userPlantId);
            if (patient == null)
                return null;

            var lastDiagnosis = patient.Diagnoses.FirstOrDefault();

            return new OthersDiagnosisViewModel
            {
                PatientId = patient.PatientId,
                TreatmentId = patient.TreatmentId,
                PatientName = patient.PatientName,
                Age = patient.Age == 0 ? null : patient.Age,
                PNumber = patient.PNumber,
                Category = patient.Category,
                OtherDetails = patient.OtherDetails,
                LastVisitDate = lastDiagnosis?.VisitDate,
                DiagnosedBy = lastDiagnosis?.DiagnosedBy ?? "",
                PlantName = lastDiagnosis?.OrgPlant?.plant_name // NEW: Plant name
            };
        }

        // NEW: GetRawMedicineDataAsync WITH PLANT FILTERING
        public async Task<object> GetRawMedicineDataAsync(int diagnosisId, int? userPlantId = null)
        {
            try
            {
                // First verify the diagnosis belongs to the user's plant
                if (userPlantId.HasValue)
                {
                    var diagnosisExists = await _db.OthersDiagnoses
                        .AnyAsync(d => d.DiagnosisId == diagnosisId && d.PlantId == userPlantId.Value);

                    if (!diagnosisExists)
                    {
                        throw new UnauthorizedAccessException("Access denied to this diagnosis.");
                    }
                }

                var rawMedicines = await _db.OthersDiagnosisMedicines
                    .Where(dm => dm.DiagnosisId == diagnosisId)
                    .Select(dm => new {
                        dm.DiagnosisMedicineId,
                        dm.DiagnosisId,
                        dm.MedItemId,
                        dm.Quantity,
                        dm.Dose,
                        dm.Instructions
                    })
                    .ToListAsync();

                var medicineIds = rawMedicines.Select(rm => rm.MedItemId).ToList();

                var medMasters = await _db.med_masters
                    .Where(mm => medicineIds.Contains(mm.MedItemId))
                    .Select(mm => new {
                        mm.MedItemId,
                        mm.MedItemName,
                        mm.CompanyName
                    })
                    .ToListAsync();

                var joinResult = from rm in rawMedicines
                                 join mm in medMasters on rm.MedItemId equals mm.MedItemId
                                 select new
                                 {
                                     rm.DiagnosisMedicineId,
                                     rm.MedItemId,
                                     mm.MedItemName,
                                     rm.Quantity,
                                     rm.Dose,
                                     rm.Instructions,
                                     mm.CompanyName
                                 };

                return new
                {
                    diagnosisId = diagnosisId,
                    rawMedicineCount = rawMedicines.Count,
                    rawMedicines = rawMedicines,
                    medMasterCount = medMasters.Count,
                    medMasters = medMasters,
                    joinResult = joinResult.ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting raw medicine data: {ex.Message}", ex);
            }
        }

        public async Task<List<MedMaster>> GetCompounderMedicinesAsync()
        {
            var medicineIds = await _db.CompounderIndentItems
                .Select(ci => ci.MedItemId)
                .Distinct()
                .ToListAsync();

            var medicines = await _db.med_masters
                .Where(m => medicineIds.Contains(m.MedItemId))
                .ToListAsync();

            return medicines;
        }

        // ======= APPROVAL METHODS WITH PLANT FILTERING =======

        // NEW: GetPendingApprovalCountAsync WITH PLANT FILTERING
        public async Task<int> GetPendingApprovalCountAsync(int? userPlantId = null)
        {
            var query = _db.OthersDiagnoses.AsQueryable();

            // NEW: Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.PlantId == userPlantId.Value);
            }

            return await query.CountAsync(d => d.ApprovalStatus == "Pending");
        }

        // NEW: GetPendingApprovalsAsync WITH PLANT FILTERING
        public async Task<List<OthersPendingApprovalViewModel>> GetPendingApprovalsAsync(int? userPlantId = null)
        {
            var query = _db.OthersDiagnoses
                .Include(d => d.Patient)
                .Include(d => d.DiagnosisDiseases)
                    .ThenInclude(dd => dd.MedDisease)
                .Include(d => d.DiagnosisMedicines)
                    .ThenInclude(dm => dm.MedMaster)
                .Include(d => d.OrgPlant) // NEW: Include plant info
                .AsQueryable();

            // NEW: Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.PlantId == userPlantId.Value);
            }

            var pendingDiagnoses = await query
                .Where(d => d.ApprovalStatus == "Pending")
                .OrderByDescending(d => d.VisitDate)
                .ThenByDescending(d => d.CreatedDate)
                .ThenByDescending(d => d.PatientId)
                .ToListAsync();

            var result = new List<OthersPendingApprovalViewModel>();

            foreach (var diagnosis in pendingDiagnoses)
            {
                var diseases = diagnosis.DiagnosisDiseases.Select(dd => new OthersDiseaseDetails
                {
                    DiseaseId = dd.DiseaseId,
                    DiseaseName = dd.MedDisease?.DiseaseName ?? "Unknown Disease",
                    DiseaseDescription = dd.MedDisease?.DiseaseDesc
                }).ToList();

                // Decrypt medicine names for approval display
                var medicines = diagnosis.DiagnosisMedicines.Select(dm => {
                    var medicineName = dm.MedMaster?.MedItemName ?? "Unknown Medicine";

                    if (!string.IsNullOrEmpty(dm.Instructions) && dm.Instructions.Contains(" - "))
                    {
                        var parts = dm.Instructions.Split(" - ", 2);
                        if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                        {
                            var decryptedName = _encryptionService.Decrypt(parts[0]);
                            if (!string.IsNullOrEmpty(decryptedName))
                            {
                                medicineName = decryptedName;
                            }
                        }
                    }

                    return new OthersMedicineDetails
                    {
                        MedItemId = dm.MedItemId,
                        MedicineName = medicineName,
                        Quantity = dm.Quantity,
                        Dose = dm.Dose,
                        Instructions = dm.Instructions,
                        CompanyName = dm.MedMaster?.CompanyName
                    };
                }).ToList();

                result.Add(new OthersPendingApprovalViewModel
                {
                    DiagnosisId = diagnosis.DiagnosisId,
                    TreatmentId = diagnosis.Patient?.TreatmentId ?? "N/A",
                    PatientName = diagnosis.Patient?.PatientName ?? "N/A",
                    Age = diagnosis.Patient?.Age == 0 ? null : diagnosis.Patient?.Age,
                    Category = diagnosis.Patient?.Category ?? "N/A",
                    VisitDate = diagnosis.VisitDate,
                    VisitType = diagnosis.VisitType,
                    DiagnosedBy = diagnosis.DiagnosedBy,
                    // DECRYPT vital signs
                    BloodPressure = _encryptionService.Decrypt(diagnosis.BloodPressure),
                    PulseRate = _encryptionService.Decrypt(diagnosis.PulseRate),
                    Sugar = _encryptionService.Decrypt(diagnosis.Sugar),
                    ApprovalStatus = diagnosis.ApprovalStatus,
                    MedicineCount = medicines.Count,
                    PlantName = diagnosis.OrgPlant?.plant_name ?? "Unknown Plant", // NEW: Plant name
                    Diseases = diseases,
                    Medicines = medicines
                });
            }

            return result;
        }

        // NEW: ApproveDiagnosisAsync WITH PLANT FILTERING
        public async Task<bool> ApproveDiagnosisAsync(int diagnosisId, string approvedBy, int? userPlantId = null)
        {
            var query = _db.OthersDiagnoses.AsQueryable();

            // NEW: Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.PlantId == userPlantId.Value);
            }

            var diagnosis = await query.FirstOrDefaultAsync(d => d.DiagnosisId == diagnosisId);
            if (diagnosis == null || diagnosis.ApprovalStatus != "Pending")
                return false;

            diagnosis.ApprovalStatus = "Approved";
            diagnosis.ApprovedBy = approvedBy;
            diagnosis.ApprovedDate = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }

        // NEW: RejectDiagnosisAsync WITH PLANT FILTERING
        public async Task<bool> RejectDiagnosisAsync(int diagnosisId, string rejectionReason, string rejectedBy, int? userPlantId = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine($"🔄 Starting rejection process for Others Diagnosis ID: {diagnosisId}");

                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    Console.WriteLine($"⚠️ Rejection reason is required for Others diagnosis {diagnosisId}");
                    return false;
                }

                var query = _db.OthersDiagnoses
                    .Include(d => d.DiagnosisMedicines) // Include medicines to restore stock
                    .AsQueryable();

                // Plant filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(d => d.PlantId == userPlantId.Value);
                }

                var diagnosis = await query.FirstOrDefaultAsync(d => d.DiagnosisId == diagnosisId);

                if (diagnosis == null || diagnosis.ApprovalStatus != "Pending")
                {
                    Console.WriteLine($"⚠️ Others diagnosis {diagnosisId} not found, not pending, or access denied for plant {userPlantId}");
                    return false;
                }

                // IMPORTANT: Restore medicine stock before rejecting using CORRECTED FIFO logic
                if (diagnosis.DiagnosisMedicines?.Any() == true)
                {
                    Console.WriteLine($"🔄 Restoring stock for {diagnosis.DiagnosisMedicines.Count} medicines due to Others diagnosis rejection");

                    foreach (var diagnosisMedicine in diagnosis.DiagnosisMedicines)
                    {
                        // CORRECTED: Use consistent FIFO logic with UpdateAvailableStockAsync
                        var indentBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == diagnosisMedicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == diagnosis.PlantId)
                            .OrderBy(b => b.ExpiryDate) // CORRECTED: Handle null expiry dates
                            .ThenBy(b => b.BatchNo) // ADDED: Secondary sort for consistency with debit logic
                            .FirstOrDefaultAsync();

                        if (indentBatch != null)
                        {
                            var oldStock = indentBatch.AvailableStock;
                            indentBatch.AvailableStock += diagnosisMedicine.Quantity;

                            Console.WriteLine($"🔄 Restored stock for Medicine ID {diagnosisMedicine.MedItemId}: {oldStock} → {indentBatch.AvailableStock} (+{diagnosisMedicine.Quantity}) due to Others diagnosis rejection (Batch: {indentBatch.BatchNo})");

                            _db.CompounderIndentBatches.Update(indentBatch);
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Could not find batch to restore stock for Medicine ID {diagnosisMedicine.MedItemId} during Others diagnosis rejection");

                            // ENHANCED: Try to find any batch for this medicine as fallback
                            var fallbackBatch = await _db.CompounderIndentBatches
                                .Include(b => b.CompounderIndentItem)
                                    .ThenInclude(i => i.CompounderIndent)
                                .Where(b => b.CompounderIndentItem.MedItemId == diagnosisMedicine.MedItemId)
                                .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == diagnosis.PlantId)
                                .FirstOrDefaultAsync();

                            if (fallbackBatch != null)
                            {
                                var oldStock = fallbackBatch.AvailableStock;
                                fallbackBatch.AvailableStock += diagnosisMedicine.Quantity;

                                Console.WriteLine($"🔄 FALLBACK: Restored stock for Medicine ID {diagnosisMedicine.MedItemId}: {oldStock} → {fallbackBatch.AvailableStock} (+{diagnosisMedicine.Quantity}) to batch {fallbackBatch.BatchNo}");

                                _db.CompounderIndentBatches.Update(fallbackBatch);
                            }
                            else
                            {
                                Console.WriteLine($"❌ CRITICAL: No batch found to restore stock for Medicine ID {diagnosisMedicine.MedItemId}. Stock may be inconsistent!");
                            }
                        }
                    }
                }

                // Update diagnosis status to rejected
                diagnosis.ApprovalStatus = "Rejected";
                diagnosis.ApprovedBy = rejectedBy;
                diagnosis.ApprovedDate = DateTime.Now;
                diagnosis.RejectionReason = rejectionReason;

                _db.OthersDiagnoses.Update(diagnosis);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"❌ Others diagnosis {diagnosisId} rejected by {rejectedBy} in plant {userPlantId}. Reason: {rejectionReason}. Stock restored successfully.");
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error rejecting Others diagnosis {diagnosisId}: {ex.Message}");
                throw; // Re-throw to let controller handle the error
            }
        }
        // NEW: ApproveAllDiagnosesAsync WITH PLANT FILTERING
        public async Task<int> ApproveAllDiagnosesAsync(List<int> diagnosisIds, string approvedBy, int? userPlantId = null)
        {
            var query = _db.OthersDiagnoses.AsQueryable();

            // NEW: Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(d => d.PlantId == userPlantId.Value);
            }

            var diagnoses = await query
                .Where(d => diagnosisIds.Contains(d.DiagnosisId) && d.ApprovalStatus == "Pending")
                .ToListAsync();

            foreach (var diagnosis in diagnoses)
            {
                diagnosis.ApprovalStatus = "Approved";
                diagnosis.ApprovedBy = approvedBy;
                diagnosis.ApprovedDate = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            return diagnoses.Count;
        }

        // ======= NEW HELPER METHODS FOR PLANT-BASED OPERATIONS =======

        /// <summary>
        /// Gets user's plant ID based on username
        /// </summary>
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        /// <summary>
        /// Checks if user is authorized to access a specific diagnosis
        /// </summary>
        public async Task<bool> IsUserAuthorizedForDiagnosisAsync(int diagnosisId, int userPlantId)
        {
            return await _db.OthersDiagnoses.AnyAsync(d => d.DiagnosisId == diagnosisId && d.PlantId == userPlantId);
        }

        //edit logic
        public async Task<OthersDiagnosisEditPermissionResult> CanEditDiagnosisAsync(int diagnosisId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Checking edit permissions for Others diagnosis {diagnosisId} in plant {userPlantId}");

                var query = _db.OthersDiagnoses
                    .Where(d => d.DiagnosisId == diagnosisId);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(d => d.PlantId == userPlantId.Value);
                }

                var diagnosis = await query.FirstOrDefaultAsync();

                if (diagnosis == null)
                {
                    Console.WriteLine($"❌ Others diagnosis {diagnosisId} not found in plant {userPlantId}");
                    return new OthersDiagnosisEditPermissionResult
                    {
                        CanEdit = false,
                        Message = "Diagnosis not found or you don't have access to it.",
                        DiagnosisExists = false,
                        IsInUserPlant = false
                    };
                }

                // Check if diagnosis can be edited (only Pending or Rejected)
                var canEdit = diagnosis.ApprovalStatus == "Pending" || diagnosis.ApprovalStatus == "Rejected";

                var result = new OthersDiagnosisEditPermissionResult
                {
                    CanEdit = canEdit,
                    ApprovalStatus = diagnosis.ApprovalStatus ?? "Unknown",
                    DiagnosisExists = true,
                    IsInUserPlant = true,
                    Message = canEdit
                        ? "Diagnosis can be edited."
                        : $"Cannot edit diagnosis with status: {diagnosis.ApprovalStatus}. Only Pending or Rejected diagnoses can be edited."
                };

                Console.WriteLine($"✅ Edit permission check completed - CanEdit: {canEdit}, Status: {diagnosis.ApprovalStatus}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking edit permissions: {ex.Message}");
                return new OthersDiagnosisEditPermissionResult
                {
                    CanEdit = false,
                    Message = "Error checking edit permissions.",
                    DiagnosisExists = false,
                    IsInUserPlant = false
                };
            }
        }

        public async Task<OthersDiagnosisEditViewModel?> GetDiagnosisForEditAsync(int diagnosisId, int? userPlantId = null)
        {
            try
            {
                Console.WriteLine($"🔍 Getting Others diagnosis {diagnosisId} for edit in plant {userPlantId}");

                var query = _db.OthersDiagnoses
                    .Include(d => d.Patient)
                    .Include(d => d.DiagnosisDiseases)
                        .ThenInclude(dd => dd.MedDisease)
                    .Include(d => d.DiagnosisMedicines)
                        .ThenInclude(dm => dm.MedMaster)
                        .ThenInclude(m => m.MedBase)
                    .Include(d => d.OrgPlant)
                    .Where(d => d.DiagnosisId == diagnosisId);

                // Plant-wise filtering
                if (userPlantId.HasValue)
                {
                    query = query.Where(d => d.PlantId == userPlantId.Value);
                }

                var diagnosis = await query.FirstOrDefaultAsync();

                if (diagnosis == null)
                {
                    Console.WriteLine($"❌ Others diagnosis {diagnosisId} not found for edit in plant {userPlantId}");
                    return null;
                }

                // Check if can be edited
                var canEdit = diagnosis.ApprovalStatus == "Pending" || diagnosis.ApprovalStatus == "Rejected";
                if (!canEdit)
                {
                    Console.WriteLine($"❌ Others diagnosis {diagnosisId} cannot be edited - Status: {diagnosis.ApprovalStatus}");
                    return null;
                }

                // Get available diseases for the plant
                var availableDiseases = await GetDiseasesAsync(userPlantId);

                // Decrypt medicine names from instructions (same logic as GetDiagnosisDetailsAsync)
                var currentMedicines = diagnosis.DiagnosisMedicines?.Select(dm => {
                    var medicineName = dm.MedMaster?.MedItemName ?? "Unknown Medicine";
                    var baseName = dm.MedMaster?.MedBase?.BaseName ?? "Not Defined";

                    // Decrypt medicine name from instructions if available
                    if (!string.IsNullOrEmpty(dm.Instructions) && dm.Instructions.Contains(" - "))
                    {
                        var parts = dm.Instructions.Split(" - ", 2);
                        if (parts.Length > 0 && _encryptionService.IsEncrypted(parts[0]))
                        {
                            var decryptedName = _encryptionService.Decrypt(parts[0]);
                            if (!string.IsNullOrEmpty(decryptedName))
                            {
                                medicineName = decryptedName;
                            }
                        }
                    }

                    return new OthersMedicineEdit
                    {
                        DiagnosisMedicineId = dm.DiagnosisMedicineId,
                        MedItemId = dm.MedItemId,
                        MedicineName = medicineName,
                        BaseName = baseName,
                        Quantity = dm.Quantity,
                        Dose = dm.Dose ?? "",
                        Instructions = dm.Instructions,
                        CompanyName = dm.MedMaster?.CompanyName
                    };
                }).ToList() ?? new List<OthersMedicineEdit>();

                // Build the edit view model
                var editModel = new OthersDiagnosisEditViewModel
                {
                    DiagnosisId = diagnosis.DiagnosisId,
                    TreatmentId = diagnosis.Patient?.TreatmentId ?? "N/A",
                    PatientName = diagnosis.Patient?.PatientName ?? "N/A",
                    Age = diagnosis.Patient?.Age == 0 ? null : diagnosis.Patient?.Age,
                    PNumber = diagnosis.Patient?.PNumber,
                    Category = diagnosis.Patient?.Category,
                    OtherDetails = diagnosis.Patient?.OtherDetails,
                    VisitDate = diagnosis.VisitDate,
                    LastVisitDate = diagnosis.LastVisitDate,
                    ApprovalStatus = diagnosis.ApprovalStatus ?? "Unknown",
                    RejectionReason = diagnosis.RejectionReason,
                    DiagnosedBy = diagnosis.DiagnosedBy,
                    VisitType = diagnosis.VisitType ?? "Regular Visitor",
                    PlantName = diagnosis.OrgPlant?.plant_name ?? "Unknown Plant",

                    // Decrypt vital signs
                    BloodPressure = _encryptionService.Decrypt(diagnosis.BloodPressure),
                    PulseRate = _encryptionService.Decrypt(diagnosis.PulseRate),
                    Sugar = _encryptionService.Decrypt(diagnosis.Sugar),
                    Remarks = diagnosis.Remarks,

                    // Selected diseases
                    SelectedDiseaseIds = diagnosis.DiagnosisDiseases?.Select(dd => dd.DiseaseId).ToList() ?? new List<int>(),
                    AvailableDiseases = availableDiseases,

                    // Current medicines with detailed info
                    CurrentMedicines = currentMedicines,

                    Patient = diagnosis.Patient
                };

                Console.WriteLine($"✅ Others diagnosis edit data loaded - Diseases: {editModel.SelectedDiseaseIds.Count}, Medicines: {editModel.CurrentMedicines.Count}");
                return editModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting Others diagnosis for edit: {ex.Message}");
                return null;
            }
        }

        public async Task<OthersDiagnosisUpdateResult> UpdateDiagnosisAsync(int diagnosisId,
            List<int> selectedDiseases, List<OthersPrescriptionMedicine> medicines,
            OthersDiagnosisViewModel basicInfo, string modifiedBy, int? userPlantId = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine($"🔄 Updating Others diagnosis {diagnosisId} by {modifiedBy} in plant {userPlantId}");

                // Get existing diagnosis with all related data
                var query = _db.OthersDiagnoses
                    .Include(d => d.DiagnosisDiseases)
                    .Include(d => d.DiagnosisMedicines)
                    .Include(d => d.Patient)
                    .Where(d => d.DiagnosisId == diagnosisId);

                if (userPlantId.HasValue)
                {
                    query = query.Where(d => d.PlantId == userPlantId.Value);
                }

                var diagnosis = await query.FirstOrDefaultAsync();

                if (diagnosis == null)
                {
                    return new OthersDiagnosisUpdateResult
                    {
                        Success = false,
                        Message = "Diagnosis not found or access denied."
                    };
                }

                // Verify can edit
                if (diagnosis.ApprovalStatus != "Pending" && diagnosis.ApprovalStatus != "Rejected")
                {
                    return new OthersDiagnosisUpdateResult
                    {
                        Success = false,
                        Message = $"Cannot edit diagnosis with status: {diagnosis.ApprovalStatus}"
                    };
                }

                var validationErrors = new List<string>();

                // Validate diseases
                if (selectedDiseases?.Any() != true)
                {
                    validationErrors.Add("At least one disease must be selected.");
                }

                // Validate medicine stock for new/modified medicines
                if (medicines?.Any() == true)
                {
                    foreach (var medicine in medicines)
                    {
                        if (medicine.IndentItemId.HasValue && medicine.IndentItemId.Value > 0)
                        {
                            var availableStock = await GetAvailableStockAsync(medicine.IndentItemId.Value, userPlantId);

                            // For existing medicines, we need to add back the current quantity to available stock
                            var existingMedicine = diagnosis.DiagnosisMedicines?
                                .FirstOrDefault(dm => dm.MedItemId == medicine.MedItemId);

                            // Only adjust stock if not rejected (rejected diagnoses don't have stock deducted)
                            if (diagnosis.ApprovalStatus != "Rejected")
                            {
                                var adjustedStock = existingMedicine != null ?
                                    availableStock + existingMedicine.Quantity : availableStock;

                                if (medicine.Quantity > adjustedStock)
                                {
                                    validationErrors.Add($"Insufficient stock for {medicine.MedicineName}. Available: {adjustedStock}, Requested: {medicine.Quantity}");
                                }
                            }
                            else
                            {
                                // For rejected diagnoses, just check current stock
                                if (medicine.Quantity > availableStock)
                                {
                                    validationErrors.Add($"Insufficient stock for {medicine.MedicineName}. Available: {availableStock}, Requested: {medicine.Quantity}");
                                }
                            }
                        }
                    }
                }

                if (validationErrors.Any())
                {
                    return new OthersDiagnosisUpdateResult
                    {
                        Success = false,
                        Message = "Validation failed.",
                        ValidationErrors = validationErrors
                    };
                }

                // Step 1: Restore stock from all existing medicines (only if not rejected)
                if (diagnosis.DiagnosisMedicines?.Any() == true && diagnosis.ApprovalStatus != "Rejected")
                {
                    foreach (var existingMedicine in diagnosis.DiagnosisMedicines)
                    {
                        // Find the batch to restore stock
                        var indentBatch = await _db.CompounderIndentBatches
                            .Include(b => b.CompounderIndentItem)
                                .ThenInclude(i => i.CompounderIndent)
                            .Where(b => b.CompounderIndentItem.MedItemId == existingMedicine.MedItemId)
                            .Where(b => b.CompounderIndentItem.CompounderIndent.plant_id == diagnosis.PlantId)
                            .OrderBy(b => b.ExpiryDate)
                            .ThenBy(b => b.BatchNo)
                            .FirstOrDefaultAsync();

                        if (indentBatch != null)
                        {
                            indentBatch.AvailableStock += existingMedicine.Quantity;
                            Console.WriteLine($"🔄 Restored {existingMedicine.Quantity} units to batch for Others medicine {existingMedicine.MedItemId}");
                        }
                    }
                }

                // Step 2: Remove existing diseases and medicines
                if (diagnosis.DiagnosisDiseases?.Any() == true)
                {
                    _db.OthersDiagnosisDiseases.RemoveRange(diagnosis.DiagnosisDiseases);
                }

                if (diagnosis.DiagnosisMedicines?.Any() == true)
                {
                    _db.OthersDiagnosisMedicines.RemoveRange(diagnosis.DiagnosisMedicines);
                }

                // Step 3: Update diagnosis basic info
                diagnosis.BloodPressure = _encryptionService.Encrypt(basicInfo.BloodPressure ?? "");
                diagnosis.PulseRate = _encryptionService.Encrypt(basicInfo.PulseRate ?? "");
                diagnosis.Sugar = _encryptionService.Encrypt(basicInfo.Sugar ?? "");
                diagnosis.Remarks = basicInfo.Remarks;
                diagnosis.LastVisitDate = basicInfo.LastVisitDate;

                // Update patient info if provided
                if (diagnosis.Patient != null && basicInfo != null)
                {
                    diagnosis.Patient.PatientName = basicInfo.PatientName;
                    diagnosis.Patient.Age = basicInfo.Age ?? 0;
                    diagnosis.Patient.PNumber = basicInfo.PNumber ?? "";
                    diagnosis.Patient.Category = basicInfo.Category ?? "";
                    diagnosis.Patient.OtherDetails = basicInfo.OtherDetails;
                }

                // Reset approval status to Pending
                diagnosis.ApprovalStatus = "Pending";
                diagnosis.ApprovedBy = null;
                diagnosis.ApprovedDate = null;
                diagnosis.RejectionReason = null;

                // Step 4: Add new diseases
                if (selectedDiseases?.Any() == true)
                {
                    var diagnosisDiseases = selectedDiseases.Select(diseaseId => new OthersDiagnosisDisease
                    {
                        DiagnosisId = diagnosisId,
                        DiseaseId = diseaseId
                    }).ToList();

                    _db.OthersDiagnosisDiseases.AddRange(diagnosisDiseases);
                }

                // Step 5: Add new medicines and adjust stock
                var affectedMedicines = 0;
                if (medicines?.Any() == true)
                {
                    var diagnosisMedicines = medicines.Select(med => new OthersDiagnosisMedicine
                    {
                        DiagnosisId = diagnosisId,
                        MedItemId = med.MedItemId,
                        Quantity = med.Quantity,
                        Dose = med.Dose ?? "",
                        Instructions = !string.IsNullOrEmpty(med.Instructions)
                            ? $"{_encryptionService.Encrypt(med.MedicineName)} - {med.Instructions}"
                            : $"{_encryptionService.Encrypt(med.MedicineName)} - {med.Dose}"
                    }).ToList();

                    _db.OthersDiagnosisMedicines.AddRange(diagnosisMedicines);

                    // Adjust stock for new medicines
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

                Console.WriteLine($"✅ Others diagnosis {diagnosisId} updated successfully - Diseases: {selectedDiseases?.Count ?? 0}, Medicines: {affectedMedicines}");

                return new OthersDiagnosisUpdateResult
                {
                    Success = true,
                    Message = "Diagnosis updated successfully. Status reset to Pending for approval.",
                    StockAdjusted = true,
                    AffectedMedicines = affectedMedicines
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error updating Others diagnosis {diagnosisId}: {ex.Message}");

                return new OthersDiagnosisUpdateResult
                {
                    Success = false,
                    Message = "Error updating diagnosis: " + ex.Message
                };
            }
        }

    }
}