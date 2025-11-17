using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class HealthProfileViewModel
    {
        public int EmpNo { get; set; }

        [Display(Name = "Exam Date")]
        public DateTime? ExamDate { get; set; }

        [Display(Name = "Food Habit")]
        public string? FoodHabit { get; set; }

        // NEW: Plant ID for plant-wise access control
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        // Flag to indicate if this is a new entry
        public bool IsNewEntry { get; set; }

        // Reference Data
        public List<RefWorkArea> ReferenceWorkAreas { get; set; } = new List<RefWorkArea>();
        public List<RefMedCondition> MedConditions { get; set; } = new List<RefMedCondition>();

        [BindNever]
        public List<HrEmployeeDependent> Dependents { get; set; } = new List<HrEmployeeDependent>();

        [BindNever]
        public List<HrEmployee> EmployeeDetails { get; set; } = new List<HrEmployee>();

        public List<DateTime> AvailableExamDates { get; set; } = new List<DateTime>();

        // Selected Data
        public List<int> SelectedWorkAreaIds { get; set; } = new List<int>();
        public List<int> SelectedConditionIds { get; set; } = new List<int>();

        // Exam-specific Data
        public List<MedExamCondition> ExamConditions { get; set; } = new List<MedExamCondition>();
        public MedGeneralExam GeneralExam { get; set; } = new MedGeneralExam();
        public List<MedWorkHistory> WorkHistories { get; set; } = new List<MedWorkHistory>();

        // NEW: Navigation property for Plant (read-only)
        [BindNever]
        public OrgPlant? OrgPlant { get; set; }

        // NEW: Helper property for plant name display
        [BindNever]
        public string PlantName => OrgPlant?.plant_name ?? "Unknown Plant";
    }
}