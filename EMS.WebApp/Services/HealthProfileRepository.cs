using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class HealthProfileRepository : IHealthProfileRepository
    {
        private readonly ApplicationDbContext _db;

        public HealthProfileRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<HealthProfileViewModel> LoadFormData(int empNo, DateTime? examDate = null, int? userPlantId = null)
        {
            // Check if employee exists and user has access
            var employeeQuery = _db.HrEmployees.Where(e => e.emp_uid == empNo);

            // Plant-wise filtering for employee access
            if (userPlantId.HasValue)
            {
                employeeQuery = employeeQuery.Where(e => e.plant_id == userPlantId.Value);
            }

            var employee = await employeeQuery.FirstOrDefaultAsync();
            if (employee == null)
            {
                return null; // Employee not found or access denied
            }

            MedExamHeader exam = null;
            int examId = 0;
            DateTime currentExamDate = DateTime.Now.Date;
            bool isNewEntry = false;

            // If examDate is provided, try to find the specific exam record
            if (examDate.HasValue)
            {
                // Convert DateTime to DateOnly for comparison
                var examDateOnly = DateOnly.FromDateTime(examDate.Value.Date);

                var examQuery = _db.MedExamHeaders
                    .Where(e => e.emp_uid == empNo &&
                               e.exam_date.HasValue &&
                               e.exam_date.Value == examDateOnly);

                // Plant-wise filtering for exam records
                if (userPlantId.HasValue)
                {
                    examQuery = examQuery.Where(e => e.PlantId == userPlantId.Value);
                }

                exam = await examQuery.FirstOrDefaultAsync();
                currentExamDate = examDate.Value.Date;

                // If no exam found for the specific date, this is a new entry for that date
                if (exam == null)
                {
                    isNewEntry = true;
                }
            }
            else
            {
                // No examDate provided - this is a new entry with current system date
                isNewEntry = true;
                currentExamDate = DateTime.Now.Date;
            }

            // Set examId if exam exists
            if (exam != null && !isNewEntry)
            {
                examId = exam.exam_id;
            }

            // Get all reference data (always needed)
            var allWorkAreas = await _db.RefWorkAreas.ToListAsync();
            var allMedConditions = await _db.RefMedConditions.ToListAsync();
            var dependents = await _db.HrEmployeeDependents
                .Where(d => d.emp_uid == empNo)
                .ToListAsync();
            var employeeDetails = await _db.HrEmployees
                .Where(d => d.emp_uid == empNo)
                .ToListAsync();

            // Initialize with empty data for new entries
            var selectedAreaIds = new List<int>();
            var selectedConditionIds = new List<int>();
            var examConditions = new List<MedExamCondition>();
            var generalExam = new MedGeneralExam { emp_uid = empNo };
            var workHistories = new List<MedWorkHistory>();
            string foodHabit = null;

            // Only load existing data if not a new entry
            if (examId > 0 && !isNewEntry)
            {
                selectedAreaIds = await _db.MedExamWorkAreas
                    .Where(m => m.exam_id == examId)
                    .Select(m => m.area_uid)
                    .ToListAsync();

                selectedConditionIds = await _db.MedExamConditions
                    .Where(c => c.exam_id == examId)
                    .Select(c => c.cond_uid)
                    .ToListAsync();

                examConditions = await _db.MedExamConditions
                    .Where(c => c.exam_id == examId)
                    .Include(c => c.RefMedCondition)
                    .ToListAsync();

                // Plant-wise filtering for general exam records
                var generalExamQuery = _db.MedGeneralExams
                    .Where(g => g.emp_uid == empNo && g.exam_id == examId);

                if (userPlantId.HasValue)
                {
                    generalExamQuery = generalExamQuery.Where(g => g.PlantId == userPlantId.Value);
                }

                var existingGeneralExam = await generalExamQuery.FirstOrDefaultAsync();

                if (existingGeneralExam != null)
                {
                    generalExam = existingGeneralExam;
                }
                else
                {
                    generalExam.exam_id = examId;
                    generalExam.PlantId = (short?)userPlantId; // Set plant ID for new entries
                }

                workHistories = await _db.MedWorkHistories
                    .Where(w => w.emp_uid == empNo && w.exam_id == examId)
                    .ToListAsync();

                foodHabit = exam?.food_habit;
            }
            else
            {
                // Set plant ID for new general exam
                generalExam.PlantId = (short?)userPlantId;
            }

            // If no work histories exist or it's a new entry, add an empty one for the form
            if (!workHistories.Any())
            {
                workHistories.Add(new MedWorkHistory
                {
                    emp_uid = empNo,
                    exam_id = examId
                });
            }

            var viewModel = new HealthProfileViewModel
            {
                EmpNo = empNo,
                ExamDate = currentExamDate,
                FoodHabit = foodHabit,
                IsNewEntry = isNewEntry,
                PlantId = (short?)userPlantId, // NEW: Set plant ID in view model

                ReferenceWorkAreas = allWorkAreas,
                SelectedWorkAreaIds = selectedAreaIds,

                MedConditions = allMedConditions,
                SelectedConditionIds = selectedConditionIds,

                Dependents = dependents,
                EmployeeDetails = employeeDetails,
                ExamConditions = examConditions,
                GeneralExam = generalExam,
                WorkHistories = workHistories
            };

            // Get all available exam dates for this employee with plant filtering
            var examHeaderQuery = _db.MedExamHeaders
                .Where(e => e.emp_uid == empNo && e.exam_date.HasValue);

            if (userPlantId.HasValue)
            {
                examHeaderQuery = examHeaderQuery.Where(e => e.PlantId == userPlantId.Value);
            }

            var examHeaders = await examHeaderQuery
                .OrderByDescending(e => e.exam_date)
                .ToListAsync();

            viewModel.AvailableExamDates = examHeaders
                .Select(e => e.exam_date!.Value.ToDateTime(TimeOnly.MinValue))
                .ToList();

            return viewModel;
        }

        public async Task<List<DateTime>> GetAvailableExamDatesAsync(int? userPlantId = null)
        {
            var examHeaderQuery = _db.MedExamHeaders
                .Where(e => e.exam_date.HasValue);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                examHeaderQuery = examHeaderQuery.Where(e => e.PlantId == userPlantId.Value);
            }

            var examHeaders = await examHeaderQuery
                .OrderByDescending(e => e.exam_date)
                .ToListAsync();

            return examHeaders
                .Select(e => e.exam_date!.Value.ToDateTime(TimeOnly.MinValue))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();
        }

        public async Task<List<DateTime>> GetAvailableExamDatesAsync(int empNo, int? userPlantId = null)
        {
            var examHeaderQuery = _db.MedExamHeaders
                .Where(e => e.emp_uid == empNo && e.exam_date.HasValue);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                examHeaderQuery = examHeaderQuery.Where(e => e.PlantId == userPlantId.Value);
            }

            var examHeaders = await examHeaderQuery
                .OrderByDescending(e => e.exam_date)
                .ToListAsync();

            return examHeaders
                .Select(e => e.exam_date!.Value.ToDateTime(TimeOnly.MinValue))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();
        }

        // UPDATED: Now searches by emp_id instead of emp_uid and includes plant filtering
        public async Task<List<string>> GetMatchingEmployeeIdsAsync(string term, int? userPlantId = null)
        {
            var employeeQuery = _db.HrEmployees
                .Where(e => e.emp_id.StartsWith(term)); // CHANGED: Now using emp_id instead of emp_uid

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                employeeQuery = employeeQuery.Where(e => e.plant_id == userPlantId.Value);
            }

            return await employeeQuery
                .OrderBy(e => e.emp_id)
                .Select(e => e.emp_id) // CHANGED: Now returning emp_id
                .Take(10)
                .ToListAsync();
        }

        public async Task SaveFormDataAsync(HealthProfileViewModel model, int? userPlantId = null)
        {
            // Use current system date if no exam date is provided
            var examDate = model.ExamDate ?? DateTime.Now.Date;
            var examDateOnly = DateOnly.FromDateTime(examDate);

            // Try to find existing exam for this employee and date with plant filtering
            var examQuery = _db.MedExamHeaders
                .Where(e => e.emp_uid == model.EmpNo &&
                           e.exam_date.HasValue &&
                           e.exam_date.Value == examDateOnly);

            // Plant-wise filtering
            if (userPlantId.HasValue)
            {
                examQuery = examQuery.Where(e => e.PlantId == userPlantId.Value);
            }

            var exam = await examQuery.FirstOrDefaultAsync();

            if (exam == null)
            {
                // Create new exam header with plant ID
                exam = new MedExamHeader
                {
                    emp_uid = model.EmpNo,
                    exam_date = examDateOnly,
                    food_habit = model.FoodHabit,
                    PlantId = (short?)userPlantId // NEW: Set plant ID
                };
                _db.MedExamHeaders.Add(exam);
                await _db.SaveChangesAsync(); // Save to get the exam_id
            }
            else
            {
                // Update existing exam header
                exam.food_habit = model.FoodHabit;
                _db.MedExamHeaders.Update(exam);
                await _db.SaveChangesAsync();
            }

            // Handle General Exam with plant filtering
            var existingGenExamQuery = _db.MedGeneralExams
                .Where(g => g.emp_uid == model.EmpNo && g.exam_id == exam.exam_id);

            if (userPlantId.HasValue)
            {
                existingGenExamQuery = existingGenExamQuery.Where(g => g.PlantId == userPlantId.Value);
            }

            var existingGenExam = await existingGenExamQuery.FirstOrDefaultAsync();

            if (existingGenExam != null)
            {
                // Update existing general exam
                existingGenExam.bp = model.GeneralExam.bp;
                existingGenExam.height_cm = model.GeneralExam.height_cm;
                existingGenExam.weight_kg = model.GeneralExam.weight_kg;
                existingGenExam.pulse = model.GeneralExam.pulse;
                existingGenExam.respiratory = model.GeneralExam.respiratory;
                existingGenExam.cns = model.GeneralExam.cns;
                existingGenExam.abdomen = model.GeneralExam.abdomen;
                existingGenExam.bmi = model.GeneralExam.bmi;
                existingGenExam.cvs = model.GeneralExam.cvs;
                existingGenExam.genito_urinary = model.GeneralExam.genito_urinary;
                existingGenExam.remarks = model.GeneralExam.remarks;
                existingGenExam.rr = model.GeneralExam.rr;
                existingGenExam.skin = model.GeneralExam.skin;
                existingGenExam.ent = model.GeneralExam.ent;
                existingGenExam.opthal = model.GeneralExam.opthal;
                existingGenExam.others = model.GeneralExam.others;
                _db.MedGeneralExams.Update(existingGenExam);
            }
            else
            {
                // Create new general exam with plant ID
                model.GeneralExam.emp_uid = model.EmpNo;
                model.GeneralExam.exam_id = exam.exam_id;
                model.GeneralExam.PlantId = (short?)userPlantId; // NEW: Set plant ID
                _db.MedGeneralExams.Add(model.GeneralExam);
            }

            // Handle Medical Conditions
            var existingConditions = await _db.MedExamConditions
                .Where(c => c.exam_id == exam.exam_id)
                .ToListAsync();
            _db.MedExamConditions.RemoveRange(existingConditions);

            if (model.SelectedConditionIds?.Any() == true)
            {
                foreach (var condId in model.SelectedConditionIds.Distinct())
                {
                    _db.MedExamConditions.Add(new MedExamCondition
                    {
                        exam_id = exam.exam_id,
                        cond_uid = condId,
                        present = true
                    });
                }
            }

            // Handle Work Areas
            var existingWorkAreas = await _db.MedExamWorkAreas
                .Where(w => w.exam_id == exam.exam_id)
                .ToListAsync();
            _db.MedExamWorkAreas.RemoveRange(existingWorkAreas);

            if (model.SelectedWorkAreaIds?.Any() == true)
            {
                foreach (var areaId in model.SelectedWorkAreaIds.Distinct())
                {
                    _db.MedExamWorkAreas.Add(new MedExamWorkArea
                    {
                        exam_id = exam.exam_id,
                        area_uid = areaId
                    });
                }
            }

            // Handle Work Histories
            if (model.WorkHistories?.Any() == true)
            {
                // Remove existing work histories for this exam
                var existingWorkHistories = await _db.MedWorkHistories
                    .Where(w => w.emp_uid == model.EmpNo && w.exam_id == exam.exam_id)
                    .ToListAsync();
                _db.MedWorkHistories.RemoveRange(existingWorkHistories);

                // Add new work histories (only non-empty job names)
                foreach (var wh in model.WorkHistories.Where(w => !string.IsNullOrWhiteSpace(w.job_name)))
                {
                    wh.emp_uid = model.EmpNo;
                    wh.exam_id = exam.exam_id;
                    wh.work_uid = 0; // Reset to 0 for new entry
                    _db.MedWorkHistories.Add(wh);
                }
            }

            await _db.SaveChangesAsync();
        }

        // NEW: Helper method to get user's plant ID
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        // NEW: Helper method to check if user is authorized for employee
        public async Task<bool> IsUserAuthorizedForEmployeeAsync(int empNo, int userPlantId)
        {
            return await _db.HrEmployees.AnyAsync(e => e.emp_uid == empNo && e.plant_id == userPlantId);
        }
    }
}