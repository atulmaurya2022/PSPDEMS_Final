using EMS.WebApp.Data;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class DoctorDiagnosisViewModel
    {
        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = "Regular Visitor";

        [Display(Name = "Employee ID")]
        public string EmpId { get; set; } = string.Empty;

        [Display(Name = "Examination Date")]
        public DateTime ExamDate { get; set; } = DateTime.Now.Date;

        [Display(Name = "Dependent")]
        public string? DependentName { get; set; } = "Self";


        // NEW: Dependent details properties
        public string? DependentRelation { get; set; }
        public int? DependentAge { get; set; }
        public string? DependentGender { get; set; }
        public DateTime? DependentDOB { get; set; }
        public bool? DependentIsActive { get; set; }

        // Helper property to get patient name (employee or dependent)
        public string PatientName =>
            !string.IsNullOrEmpty(DependentName) && DependentName != "Self"
                ? DependentName
                : Employee?.emp_name ?? "N/A";

        // Helper property to check if this is for a dependent
        public bool IsForDependent =>
            !string.IsNullOrEmpty(DependentName) && DependentName != "Self";


        // UPDATED: Patient Status field - now nullable with no default value
        [Display(Name = "Patient Status")]
        public string? PatientStatus { get; set; }

        // Employee Details
        public HrEmployee? Employee { get; set; }

        // Health Profile Data
        public List<RefMedCondition> MedConditions { get; set; } = new();
        public List<int> SelectedConditionIds { get; set; } = new();

        // Vital Signs
        [Display(Name = "Blood Pressure")]
        public string? BloodPressure { get; set; }

        [Display(Name = "Pulse")]
        public string? Pulse { get; set; }

        [Display(Name = "Temperature")]
        public string? Temperature { get; set; }

        // For Prescription Modal
        public List<MedDisease> AvailableDiseases { get; set; } = new();
        public List<MedMaster> AvailableMedicines { get; set; } = new();
        public List<DiagnosisEntry> PreviousDiagnoses { get; set; } = new();

        // Current Prescription
        public List<int> SelectedDiseaseIds { get; set; } = new();
        public List<PrescriptionMedicine> PrescriptionMedicines { get; set; } = new();

        // Visit Type Options for Dropdown
        public List<string> VisitTypeOptions { get; set; } = new()
        {
            "Regular Visitor",
            "First Aid or Emergency"
        };

        // Patient Status Options for Dropdown
        public List<string> PatientStatusOptions { get; set; } = new()
        {
            "On Duty",
            "Off Duty",
            "From BPL School",
            "Guest House"
        };
    }

    public class PrescriptionDetailsViewModel
    {
        public int PrescriptionId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Plant { get; set; } = string.Empty;
        public DateTime PrescriptionDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? BloodPressure { get; set; }
        public string? Pulse { get; set; }
        public string? Temperature { get; set; }
        public string? Remarks { get; set; }
        public string? PatientStatus { get; set; }

        public List<PrescriptionDiseaseDetails> Diseases { get; set; } = new();
        public List<PrescriptionMedicineDetails> Medicines { get; set; } = new();
    }

    public class PrescriptionDiseaseDetails
    {
        public int DiseaseId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public string? DiseaseDescription { get; set; }
    }

    public class PrescriptionMedicineDetails
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BaseName { get; set; }
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string? CompanyName { get; set; }
    }

    // ======= NEW APPROVAL VIEW MODELS =======

    public class PendingApprovalViewModel
    {
        public int PrescriptionId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Plant { get; set; } = string.Empty;
        public DateTime PrescriptionDate { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string? BloodPressure { get; set; }
        public string? Pulse { get; set; }
        public string? Temperature { get; set; }
        public string ApprovalStatus { get; set; } = "Pending";
        public int MedicineCount { get; set; }
        public string? PatientStatus { get; set; }
        public string? Remarks { get; set; }

        // NEW: Patient/Dependent information properties
        public string? DependentName { get; set; }
        public string PatientName => !string.IsNullOrEmpty(DependentName) && DependentName != "Self"
            ? DependentName
            : EmployeeName;

        public string PatientType => !string.IsNullOrEmpty(DependentName) && DependentName != "Self"
            ? "Dependent"
            : "Employee";

        public bool IsForDependent => !string.IsNullOrEmpty(DependentName) && DependentName != "Self";

        public List<PrescriptionDiseaseDetails> Diseases { get; set; } = new();
        public List<PrescriptionMedicineDetails> Medicines { get; set; } = new();
    }
    public class DiagnosisEntry
    {
        public int DiagnosisId { get; set; }
        public string DiagnosisName { get; set; } = string.Empty;
        public DateTime LastVisitDate { get; set; }
        public string EmpId { get; set; } = string.Empty;
    }

    // UPDATED: Enhanced PrescriptionMedicine to include batch tracking with ID-Name format
    public class PrescriptionMedicine
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;

        // NEW: Track which batch/stock item is being used
        public int? IndentItemId { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? AvailableStock { get; set; }

        // UPDATED: Helper properties for display with ID-Name-Batch format
        public string DisplayName => !string.IsNullOrEmpty(BatchNo) ?
            $"{MedItemId} - {MedicineName} - {BatchNo}" :
            $"{MedItemId} - {MedicineName}";

        public string ExpiryInfo => ExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A";
        public int DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now.Date).TotalDays : int.MaxValue;
        public bool IsNearExpiry => DaysToExpiry <= 30 && DaysToExpiry >= 0;
        public bool IsExpired => DaysToExpiry < 0;

        // NEW: Property to get clean medicine name without ID
        public string CleanMedicineName => MedicineName;
    }

    // NEW: Medicine dropdown item with batch and stock information
    public class MedicineDropdownItem
    {
        public int IndentItemId { get; set; }
        public int MedItemId { get; set; }
        public string MedItemName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int AvailableStock { get; set; }

        // UPDATED: Display properties with ID-Name-Batch format
        public string DisplayText => $"{MedItemId} - {MedItemName} - {BatchNo}";
        public string StockInfo => $"Stock: {AvailableStock}";
        public string ExpiryInfo => ExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A";
        public int DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now.Date).TotalDays : int.MaxValue;

        // CSS classes for styling based on expiry
        public string ExpiryClass => DaysToExpiry switch
        {
            < 0 => "text-danger", // Expired
            <= 7 => "text-warning", // Expires within a week
            <= 30 => "text-info", // Expires within a month
            _ => "text-success" // Good
        };

        public string ExpiryLabel => DaysToExpiry switch
        {
            < 0 => "EXPIRED",
            <= 7 => $"Expires in {DaysToExpiry} days",
            <= 30 => $"Expires in {DaysToExpiry} days",
            _ => $"Expires: {ExpiryInfo}"
        };
    }

    // NEW: Stock validation result
    public class StockValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int AvailableStock { get; set; }
        public int RequestedQuantity { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
    }

    // NEW: Disease dropdown item with ID-Name format
    public class DiseaseDropdownItem
    {
        public int DiseaseId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public string DisplayText => $"{DiseaseId} - {DiseaseName}";
    }

    public class EmployeeDiagnosisListViewModel
    {
        public int PrescriptionId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Plant { get; set; } = string.Empty;
        public DateTime PrescriptionDate { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public int DiseaseCount { get; set; }
        public int MedicineCount { get; set; }
        public string? BloodPressure { get; set; }
        public string? Pulse { get; set; }
        public string? Temperature { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string? RejectionReason { get; set; }
        public string? Remarks { get; set; }
        // Patient Status in list view
        public string? PatientStatus { get; set; }
        // NEW: Patient information properties
        public string? DependentName { get; set; }
        public string PatientName => !string.IsNullOrEmpty(DependentName) && DependentName != "Self"
            ? DependentName
            : EmployeeName ?? "N/A";

        public string PatientType => !string.IsNullOrEmpty(DependentName) && DependentName != "Self"
            ? "Dependent"
            : "Employee";

        public bool IsForDependent => !string.IsNullOrEmpty(DependentName) && DependentName != "Self";
    }
}