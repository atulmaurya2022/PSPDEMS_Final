using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EMS.WebApp.Services
{
    public class ImmunizationRepository : IImmunizationRepository
    {
        private readonly ApplicationDbContext _db;

        public ImmunizationRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ImmunizationViewModel> LoadFormData(int empNo, int? userPlantId = null)
        {
            // Check if employee exists and user has access
            var employee = await _db.HrEmployees
                .Where(e => e.emp_uid == empNo)
                .Where(e => !userPlantId.HasValue || e.plant_id == userPlantId.Value)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return null; // Employee not found or access denied
            }

            // Get all reference data
            var immunizationTypes = await GetImmunizationTypesAsync();

            var dependents = await _db.HrEmployeeDependents
                .Where(d => d.emp_uid == empNo)
                .ToListAsync();

            var employeeDetails = await _db.HrEmployees
                .Include(e => e.org_department)
                .Where(d => d.emp_uid == empNo)
                .ToListAsync();

            // Get existing immunization records
            var existingRecords = await GetExistingRecordsAsync(empNo, userPlantId);

            var viewModel = new ImmunizationViewModel
            {
                EmpNo = empNo,
                IsNewEntry = true,
                PlantId = (short?)userPlantId,
                ImmunizationTypes = immunizationTypes,
                Dependents = dependents,
                EmployeeDetails = employeeDetails,
                ExistingRecords = existingRecords
            };

            return viewModel;
        }

        public async Task SaveImmunizationRecordAsync(ImmunizationViewModel model, int? userPlantId = null, string? userName = null)
        {
            if (!model.ImmunizationTypeId.HasValue || string.IsNullOrEmpty(model.PatientName))
            {
                throw new ArgumentException("Immunization Type and Patient Name are required.");
            }

            MedImmunizationRecord record;

            // Check if an INCOMPLETE record already exists
            var existingIncompleteRecord = await FindIncompleteRecordAsync(
                model.EmpNo,
                model.ImmunizationTypeId.Value,
                model.PatientName,
                userPlantId);

            if (existingIncompleteRecord != null)
            {
                // Use existing incomplete record for update
                record = existingIncompleteRecord;

                // Get next dose information
                var nextDoseInfo = GetNextDoseInfo(record);

                if (nextDoseInfo.IsComplete)
                {
                    throw new InvalidOperationException("All doses for this immunization have been completed.");
                }

                // Allow updating any dose up to and including the next dose
                ValidateLogicalDoseSequence(model);

                // Update all doses from the model
                UpdateAllDoses(record, model);

                // Update other fields
                record.patient_name = model.PatientName;
                record.relationship = model.Relationship;
                record.remarks = model.Remarks;

                // Update audit fields
                record.updated_date = DateTime.Now;
                record.updated_by = userName;

                _db.MedImmunizationRecords.Update(record);
            }
            else if (model.RecordId.HasValue)
            {
                // Update existing record (manual edit scenario)
                record = await _db.MedImmunizationRecords
                    .Where(r => r.immun_record_uid == model.RecordId.Value)
                    .Where(r => !userPlantId.HasValue || r.plant_id == userPlantId.Value)
                    .FirstOrDefaultAsync();

                if (record == null)
                {
                    throw new UnauthorizedAccessException("Record not found or access denied.");
                }

                // Get next dose information
                var nextDoseInfo = GetNextDoseInfo(record);

                // Allow updating any dose up to the next required dose
                ValidateLogicalDoseSequence(model);

                // Update all doses
                UpdateAllDoses(record, model);

                // Update other fields
                record.patient_name = model.PatientName;
                record.relationship = model.Relationship;
                record.remarks = model.Remarks;
                record.updated_date = DateTime.Now;
                record.updated_by = userName;

                _db.MedImmunizationRecords.Update(record);
            }
            else
            {
                // Create new record
                record = new MedImmunizationRecord
                {
                    emp_uid = model.EmpNo,
                    immun_type_uid = model.ImmunizationTypeId.Value,
                    patient_name = model.PatientName,
                    relationship = model.Relationship,
                    remarks = model.Remarks,
                    plant_id = (short?)userPlantId,
                    created_date = DateTime.Now,
                    updated_date = DateTime.Now,
                    created_by = userName,
                    updated_by = userName
                };

                // For new record, set all provided doses
                UpdateAllDoses(record, model);

                _db.MedImmunizationRecords.Add(record);
            }

            await _db.SaveChangesAsync();
        }

        // New method to validate logical sequence (doses shouldn't skip)
        private void ValidateLogicalDoseSequence(ImmunizationViewModel model)
        {
            // Check that doses are not skipped (e.g., can't have dose 3 without dose 2)
            if (model.Dose2Date.HasValue && !model.Dose1Date.HasValue)
                throw new InvalidOperationException("Cannot set 2nd dose without 1st dose.");

            if (model.Dose3Date.HasValue && !model.Dose2Date.HasValue)
                throw new InvalidOperationException("Cannot set 3rd dose without 2nd dose.");

            if (model.Dose4Date.HasValue && !model.Dose3Date.HasValue)
                throw new InvalidOperationException("Cannot set 4th dose without 3rd dose.");

            if (model.Dose5Date.HasValue && !model.Dose4Date.HasValue)
                throw new InvalidOperationException("Cannot set 5th dose without 4th dose.");

            if (model.BoosterDoseDate.HasValue && !model.Dose5Date.HasValue)
                throw new InvalidOperationException("Cannot set Booster dose without 5th dose.");
        }

        // New method to update all doses at once
        private void UpdateAllDoses(MedImmunizationRecord record, ImmunizationViewModel model)
        {
            record.dose_1_date = model.Dose1Date.HasValue ? DateOnly.FromDateTime(model.Dose1Date.Value) : null;
            record.dose_2_date = model.Dose2Date.HasValue ? DateOnly.FromDateTime(model.Dose2Date.Value) : null;
            record.dose_3_date = model.Dose3Date.HasValue ? DateOnly.FromDateTime(model.Dose3Date.Value) : null;
            record.dose_4_date = model.Dose4Date.HasValue ? DateOnly.FromDateTime(model.Dose4Date.Value) : null;
            record.dose_5_date = model.Dose5Date.HasValue ? DateOnly.FromDateTime(model.Dose5Date.Value) : null;
            record.booster_dose_date = model.BoosterDoseDate.HasValue ? DateOnly.FromDateTime(model.BoosterDoseDate.Value) : null;
        }
        public async Task<MedImmunizationRecord?> FindExistingRecordAsync(int empNo, int immunizationTypeId, string patientName, int? userPlantId = null)
        {
            var query = _db.MedImmunizationRecords.AsQueryable();

            query = query.Where(r => r.emp_uid == empNo &&
                                   r.immun_type_uid == immunizationTypeId &&
                                   r.patient_name == patientName);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(r => r.plant_id == userPlantId.Value);
            }

            return await query.FirstOrDefaultAsync();
        }

        public NextDoseInfo GetNextDoseInfo(MedImmunizationRecord record)
        {
            if (!record.dose_1_date.HasValue)
            {
                return new NextDoseInfo
                {
                    DoseNumber = 1,
                    DoseName = "1st Dose",
                    IsComplete = false,
                    DisplayText = "Ready for 1st Dose"
                };
            }

            if (!record.dose_2_date.HasValue)
            {
                return new NextDoseInfo
                {
                    DoseNumber = 2,
                    DoseName = "2nd Dose",
                    IsComplete = false,
                    DisplayText = "Ready for 2nd Dose"
                };
            }

            if (!record.dose_3_date.HasValue)
            {
                return new NextDoseInfo
                {
                    DoseNumber = 3,
                    DoseName = "3rd Dose",
                    IsComplete = false,
                    DisplayText = "Ready for 3rd Dose"
                };
            }

            if (!record.dose_4_date.HasValue)
            {
                return new NextDoseInfo
                {
                    DoseNumber = 4,
                    DoseName = "4th Dose",
                    IsComplete = false,
                    DisplayText = "Ready for 4th Dose"
                };
            }

            if (!record.dose_5_date.HasValue)
            {
                return new NextDoseInfo
                {
                    DoseNumber = 5,
                    DoseName = "5th Dose",
                    IsComplete = false,
                    DisplayText = "Ready for 5th Dose"
                };
            }

            if (!record.booster_dose_date.HasValue)
            {
                return new NextDoseInfo
                {
                    DoseNumber = 6,
                    DoseName = "Booster Dose",
                    IsComplete = false,
                    DisplayText = "Ready for Booster Dose"
                };
            }

            return new NextDoseInfo
            {
                DoseNumber = 0,
                DoseName = "Complete",
                IsComplete = true,
                DisplayText = "All doses completed"
            };
        }

        public async Task<List<RefImmunizationType>> GetImmunizationTypesAsync()
        {
            return await _db.RefImmunizationTypes
                .OrderBy(t => t.immun_type_name)
                .ToListAsync();
        }

        public async Task<List<MedImmunizationRecord>> GetExistingRecordsAsync(int empNo, int? userPlantId = null, int? immunizationTypeId = null)
        {
            var query = _db.MedImmunizationRecords.AsQueryable();

            // Apply filtering
            query = query.Where(r => r.emp_uid == empNo);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(r => r.plant_id == userPlantId.Value);
            }

            // Immunization type filtering
            if (immunizationTypeId.HasValue)
            {
                query = query.Where(r => r.immun_type_uid == immunizationTypeId.Value);
            }

            // Apply includes and ordering - show all records including completed ones
            return await query
                .Include(r => r.RefImmunizationType)
                .OrderByDescending(r => r.created_date)
                .ToListAsync();
        }
        public async Task<List<string>> GetMatchingEmployeeIdsAsync(string term, int? userPlantId = null)
        {
            var query = _db.HrEmployees.AsQueryable();

            query = query.Where(e => e.emp_id.StartsWith(term));

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

        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .Where(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active)
                .FirstOrDefaultAsync();

            return user?.plant_id;
        }

        public async Task<bool> IsUserAuthorizedForEmployeeAsync(int empNo, int userPlantId)
        {
            return await _db.HrEmployees
                .AnyAsync(e => e.emp_uid == empNo && e.plant_id == userPlantId);
        }

        public async Task<bool> DeleteImmunizationRecordAsync(int recordId, int? userPlantId = null)
        {
            var record = await _db.MedImmunizationRecords
                .Where(r => r.immun_record_uid == recordId)
                .Where(r => !userPlantId.HasValue || r.plant_id == userPlantId.Value)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                return false;
            }

            _db.MedImmunizationRecords.Remove(record);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<MedImmunizationRecord?> GetImmunizationRecordAsync(int recordId, int? userPlantId = null)
        {
            var query = _db.MedImmunizationRecords.AsQueryable();

            query = query.Where(r => r.immun_record_uid == recordId);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(r => r.plant_id == userPlantId.Value);
            }

            return await query
                .Include(r => r.RefImmunizationType)
                .FirstOrDefaultAsync();
        }

        public async Task<MedImmunizationRecord?> FindIncompleteRecordAsync(int empNo, int immunizationTypeId, string patientName, int? userPlantId = null)
        {
            var query = _db.MedImmunizationRecords.AsQueryable();

            query = query.Where(r => r.emp_uid == empNo &&
                                   r.immun_type_uid == immunizationTypeId &&
                                   r.patient_name == patientName);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(r => r.plant_id == userPlantId.Value);
            }

            var record = await query.FirstOrDefaultAsync();

            // Only return the record if it's not complete
            if (record != null)
            {
                var nextDoseInfo = GetNextDoseInfo(record);
                if (nextDoseInfo.IsComplete)
                {
                    return null; // Record is complete, allow new record creation
                }
            }

            return record;
        }
    }
}