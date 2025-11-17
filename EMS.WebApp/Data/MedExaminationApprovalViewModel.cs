using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class MedExaminationApprovalViewModel
    {
        // Filter Properties
        [Display(Name = "Medical Examination Category")]
        public int? SelectedCategoryId { get; set; }

        [Display(Name = "Medical Examination Category Scheduled Months")]
        public string? ScheduledMonths { get; set; }

        [Display(Name = "Test Location")]
        public int? SelectedLocationId { get; set; }

        [Display(Name = "Approval Status")]
        public string? SelectedApprovalStatus { get; set; } // null = all statuses

        // Plant ID for plant-wise access control
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        // Reference Data for Filters
        public List<MedExamCategory> ExamCategories { get; set; } = new List<MedExamCategory>();

        public List<Location> TestLocations { get; set; } = new List<Location>();

        // Updated status list - removed "UnApproved" since we're changing back to "Pending"
        public List<string> ApprovalStatuses { get; set; } = new List<string>
        {
            "Pending",
            "Approved",
            "Rejected"
        };

        // Grid Data
        public List<MedExaminationApprovalItemViewModel> ApprovalItems { get; set; } = new List<MedExaminationApprovalItemViewModel>();

        // Selected items for bulk operations
        public List<int> SelectedApprovalIds { get; set; } = new List<int>();

        // Helper property for currently selected category details
        [BindNever]
        public MedExamCategory? SelectedCategory => ExamCategories.FirstOrDefault(c => c.CatId == SelectedCategoryId);

        [BindNever]
        public string SelectedCategoryScheduledMonths => SelectedCategory?.MonthsSched ?? "";

        // Helper property for currently selected location details
        [BindNever]
        public Location? SelectedLocation => TestLocations.FirstOrDefault(l => l.LocationId == SelectedLocationId);

        [BindNever]
        public string SelectedLocationName => SelectedLocation?.LocationName ?? "";

        // Plant information
        [BindNever]
        public OrgPlant? OrgPlant { get; set; }

        [BindNever]
        public string PlantName => OrgPlant?.plant_name ?? "Unknown Plant";

        // Status counts for display
        [BindNever]
        public int PendingCount => ApprovalItems.Count(x => x.ApprovalStatus == "Pending");

        [BindNever]
        public int ApprovedCount => ApprovalItems.Count(x => x.ApprovalStatus == "Approved");

        [BindNever]
        public int RejectedCount => ApprovalItems.Count(x => x.ApprovalStatus == "Rejected");
    }

    public class MedExaminationApprovalItemViewModel
    {
        public int ApprovalId { get; set; }
        public int ResultId { get; set; }
        public int EmpUid { get; set; }
        public string EmployeeNo { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public int? LocationId { get; set; }
        public string TestLocation { get; set; } = "";
        public DateTime? LastTestDate { get; set; }
        public string Department { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public int? Age { get; set; }
        public string ApprovalStatus { get; set; } = "";
        public DateTime? ApprovedOn { get; set; }
        public string? ApprovedBy { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsSelected { get; set; }

        // Helper properties
        public string LastTestDateFormatted => LastTestDate?.ToString("dd-MMM-yyyy") ?? "N/A";
        public string CategoryAge => $"{CategoryName}{(Age.HasValue ? $" / {Age}" : "")}";
        public string StatusBadgeClass => ApprovalStatus?.ToLower() switch
        {
            "approved" => "badge bg-success",
            "rejected" => "badge bg-danger",
            "pending" => "badge bg-warning text-dark",
            _ => "badge bg-secondary"
        };

        // Helper property to determine if this item can be approved/rejected (only pending items)
        public bool CanApproveOrReject => ApprovalStatus == "Pending";

        // Helper property to determine if this item can be un-approved (only approved items)
        public bool CanUnApprove => ApprovalStatus == "Approved";
    }
}