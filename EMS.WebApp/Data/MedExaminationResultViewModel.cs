using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class MedExaminationResultViewModel
    {
        public int? ResultId { get; set; }

        public int EmpNo { get; set; }

        [Display(Name = "Medical Examination Category")]
        public int? CatId { get; set; }

        [Display(Name = "Last Check Up Date")]
        public DateTime? LastCheckupDate { get; set; }

        [Display(Name = "Test Date")]
        public DateTime? TestDate { get; set; }

        [Display(Name = "Test Location")]
        public int? LocationId { get; set; }

        [Display(Name = "Result")]
        public string? Result { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Plant ID for plant-wise access control
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        // Flag to indicate if this is a new entry
        public bool IsNewEntry { get; set; }

        // Creator information
        public string? CreatedBy { get; set; }

        // Approval information
        public string? ApprovalStatus { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? RejectionReason { get; set; }

        // Permission flags
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // Employee Details (read-only)
        [BindNever]
        public HrEmployee? EmployeeDetails { get; set; }

        // Reference Data
        public List<MedExamCategory> ExamCategories { get; set; } = new List<MedExamCategory>();

        // Available test locations from database
        public List<Location> TestLocations { get; set; } = new List<Location>();

        // Available result options
        public List<string> ResultOptions { get; set; } = new List<string>
        {
            "Yes",
            "No"
        };

        // Navigation property for Plant (read-only)
        [BindNever]
        public OrgPlant? OrgPlant { get; set; }

        // Helper property for plant name display
        [BindNever]
        public string PlantName => OrgPlant?.plant_name ?? "Unknown Plant";

        // Helper property for employee name display
        [BindNever]
        public string EmployeeName => EmployeeDetails?.emp_name ?? "Unknown Employee";

        // Helper property for employee ID display
        [BindNever]
        public string EmployeeId => EmployeeDetails?.emp_id ?? "Unknown ID";

        // Helper properties for employee details display
        [BindNever]
        public string Gender => EmployeeDetails?.emp_Gender == "M" ? "Male" :
                               EmployeeDetails?.emp_Gender == "F" ? "Female" :
                               EmployeeDetails?.emp_Gender == "O" ? "Other" : "";

        [BindNever]
        public string DOB => EmployeeDetails?.emp_DOB?.ToString("dd-MMM-yyyy") ?? "";

        [BindNever]
        public int Age
        {
            get
            {
                if (EmployeeDetails?.emp_DOB.HasValue == true)
                {
                    var today = DateOnly.FromDateTime(DateTime.Today);
                    var birthDate = EmployeeDetails.emp_DOB.Value;
                    int age = today.Year - birthDate.Year;
                    if (today < birthDate.AddYears(age))
                        age--;
                    return age;
                }
                return 0;
            }
        }

        [BindNever]
        public string BloodGroup => EmployeeDetails?.emp_blood_Group ?? "";

        [BindNever]
        public string Department => EmployeeDetails?.org_department?.dept_name ?? "";

        [BindNever]
        public string Designation => EmployeeDetails?.org_employee_category?.emp_category_name ?? "";

        // Helper property for category name display
        [BindNever]
        public string CategoryName => ExamCategories.FirstOrDefault(c => c.CatId == CatId)?.CatName ?? "";

        // Helper property for location name display
        [BindNever]
        public string TestLocationName => TestLocations.FirstOrDefault(l => l.LocationId == LocationId)?.LocationName ?? "";
    }

    // ViewModel for the list page
    public class MedExaminationResultListViewModel
    {
        public List<MedExaminationResultListItemViewModel> Results { get; set; } = new List<MedExaminationResultListItemViewModel>();
        public string SearchTerm { get; set; } = "";
        public int TotalCount => Results.Count;
    }

    // ViewModel for individual items in the list
    public class MedExaminationResultListItemViewModel
    {
        public int ResultId { get; set; }
        public int EmpUid { get; set; }
        public string EmployeeId { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public DateTime? TestDate { get; set; }
        public string TestDateFormatted { get; set; } = "";
        public string TestLocationName { get; set; } = "";
        public string Result { get; set; } = "";
        public string CreatedBy { get; set; } = "";
        public string ApprovalStatus { get; set; } = "Pending";

        // Permission flags
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}