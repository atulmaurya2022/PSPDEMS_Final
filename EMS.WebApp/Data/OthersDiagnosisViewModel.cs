
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class OthersDiagnosisListViewModel
    {
        public int DiagnosisId { get; set; }

        [Display(Name = "Treatment ID")]
        public string TreatmentId { get; set; } = string.Empty;

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        public decimal? Age { get; set; }

        public string Category { get; set; } = string.Empty;

        [Display(Name = "Visit Date")]
        public DateTime VisitDate { get; set; }

        [Display(Name = "Diagnosed By")]
        public string DiagnosedBy { get; set; } = string.Empty;

        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = string.Empty;

        [Display(Name = "Approval Status")]
        public string ApprovalStatus { get; set; } = string.Empty;

        // NEW: Plant information
        [Display(Name = "Plant")]
        public string PlantName { get; set; } = string.Empty;
    }

    public class OthersPendingApprovalViewModel
    {
        public int DiagnosisId { get; set; }

        [Display(Name = "Treatment ID")]
        public string TreatmentId { get; set; } = string.Empty;

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        public decimal? Age { get; set; }

        public string Category { get; set; } = string.Empty;

        [Display(Name = "Visit Date")]
        public DateTime VisitDate { get; set; }

        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = string.Empty;

        [Display(Name = "Diagnosed By")]
        public string DiagnosedBy { get; set; } = string.Empty;

        [Display(Name = "Blood Pressure")]
        public string? BloodPressure { get; set; }

        [Display(Name = "Pulse Rate")]
        public string? PulseRate { get; set; }

        public string? Sugar { get; set; }

        [Display(Name = "Approval Status")]
        public string ApprovalStatus { get; set; } = string.Empty;

        [Display(Name = "Medicine Count")]
        public int MedicineCount { get; set; }

        // NEW: Plant information
        [Display(Name = "Plant")]
        public string PlantName { get; set; } = string.Empty;

        // Navigation properties for detailed information
        public List<OthersDiseaseDetails> Diseases { get; set; } = new List<OthersDiseaseDetails>();
        public List<OthersMedicineDetails> Medicines { get; set; } = new List<OthersMedicineDetails>();
    }

    public class OthersDiagnosisViewModel
    {
        public int? DiagnosisId { get; set; } // NEW: Added DiagnosisId property for edit scenarios

        public int? PatientId { get; set; }

        [Display(Name = "Treatment ID")]
        public string TreatmentId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Patient name is required")]
        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        [Range(0, 120, ErrorMessage = "Age must be between 0 and 120")]
        public decimal? Age { get; set; }

        [Display(Name = "Phone Number")]
        public string? PNumber { get; set; }

        public string? Category { get; set; }

        [Display(Name = "Other Details")]
        public string? OtherDetails { get; set; }

        [Display(Name = "Last Visit Date")]
        public DateTime? LastVisitDate { get; set; }

        [Display(Name = "Blood Pressure")]
        public string? BloodPressure { get; set; }

        [Display(Name = "Pulse Rate")]
        public string? PulseRate { get; set; }

        public string? Sugar { get; set; }

        public string? Remarks { get; set; }

        [Required(ErrorMessage = "Diagnosed By is required")]
        [Display(Name = "Diagnosed By")]
        public string DiagnosedBy { get; set; } = string.Empty;

        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = "Regular Visitor";

        // NEW: Plant information (for display purposes in edit scenarios)
        [Display(Name = "Plant")]
        public string? PlantName { get; set; }

        // Disease selection
        [Display(Name = "Select Diseases")]
        public List<int>? SelectedDiseaseIds { get; set; }

        public List<MedDisease> AvailableDiseases { get; set; } = new List<MedDisease>();

        // Medicine prescription
        public List<OthersPrescriptionMedicine>? PrescriptionMedicines { get; set; }

        public List<MedMaster> AvailableMedicines { get; set; } = new List<MedMaster>();
    }

    public class OthersDiagnosisDetailsViewModel
    {
        public int DiagnosisId { get; set; }

        [Display(Name = "Treatment ID")]
        public string TreatmentId { get; set; } = string.Empty;

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        public decimal? Age { get; set; }

        [Display(Name = "Phone Number")]
        public string PNumber { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        [Display(Name = "Other Details")]
        public string? OtherDetails { get; set; }

        [Display(Name = "Visit Date")]
        public DateTime VisitDate { get; set; }

        [Display(Name = "Last Visit Date")]
        public DateTime? LastVisitDate { get; set; }

        [Display(Name = "Blood Pressure")]
        public string? BloodPressure { get; set; }

        [Display(Name = "Pulse Rate")]
        public string? PulseRate { get; set; }

        public string? Sugar { get; set; }

        public string? Remarks { get; set; }

        [Display(Name = "Diagnosed By")]
        public string DiagnosedBy { get; set; } = string.Empty;

        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = string.Empty;

        [Display(Name = "Approval Status")]
        public string ApprovalStatus { get; set; } = string.Empty;

        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved Date")]
        public DateTime? ApprovedDate { get; set; }

        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        // NEW: Plant information
        [Display(Name = "Plant")]
        public string PlantName { get; set; } = string.Empty;

        public List<OthersDiseaseDetails> Diseases { get; set; } = new List<OthersDiseaseDetails>();
        public List<OthersMedicineDetails> Medicines { get; set; } = new List<OthersMedicineDetails>();
    }

    public class OthersPrescriptionMedicine
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Dose { get; set; }
        public string? Instructions { get; set; }
        public int? IndentItemId { get; set; } // For batch tracking
        public string? BatchNo { get; set; } // For stock validation
    }

    public class OthersStockValidationResult
    {
        public bool IsValid { get; set; }
        public int AvailableStock { get; set; }
        public int RequestedQuantity { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    public class OthersDiseaseDetails
    {
        public int DiseaseId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public string? DiseaseDescription { get; set; }
    }

    public class OthersMedicineDetails
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string? CompanyName { get; set; }
    }

    
    public class MedicineStockInfo
    {
        public int IndentItemId { get; set; }
        public int MedItemId { get; set; }
        public string MedItemName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int AvailableStock { get; set; }
        public string BaseName { get; set; } = string.Empty;

        // Computed properties for UI display
        public string ExpiryDateFormatted => ExpiryDate?.ToString("dd/MM/yyyy") ?? "Not Set";

        public int DaysToExpiry => ExpiryDate.HasValue
            ? (int)(ExpiryDate.Value.Date - DateTime.Today).TotalDays
            : int.MaxValue;
    }
    // ViewModel for editing diagnosis with permission checks
    public class OthersDiagnosisEditPermissionResult
    {
        public bool CanEdit { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public bool DiagnosisExists { get; set; }
        public bool IsInUserPlant { get; set; }
    }

    public class OthersDiagnosisEditViewModel
    {
        public int DiagnosisId { get; set; }

        // Patient Information (Read-only in edit)
        [Display(Name = "Treatment ID")]
        public string TreatmentId { get; set; } = string.Empty;

        [Display(Name = "Patient Name")]
        public string PatientName { get; set; } = string.Empty;

        public decimal? Age { get; set; }

        [Display(Name = "Phone Number")]
        public string? PNumber { get; set; }

        public string? Category { get; set; }

        [Display(Name = "Other Details")]
        public string? OtherDetails { get; set; }

        // Diagnosis Information
        [Display(Name = "Visit Date")]
        public DateTime VisitDate { get; set; }

        [Display(Name = "Last Visit Date")]
        public DateTime? LastVisitDate { get; set; }

        [Display(Name = "Blood Pressure")]
        public string? BloodPressure { get; set; }

        [Display(Name = "Pulse Rate")]
        public string? PulseRate { get; set; }

        public string? Sugar { get; set; }

        public string? Remarks { get; set; }

        [Display(Name = "Diagnosed By")]
        public string DiagnosedBy { get; set; } = string.Empty;

        [Display(Name = "Visit Type")]
        public string VisitType { get; set; } = string.Empty;

        [Display(Name = "Approval Status")]
        public string ApprovalStatus { get; set; } = string.Empty;

        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        [Display(Name = "Plant")]
        public string PlantName { get; set; } = string.Empty;

        // Current selections for editing
        [Display(Name = "Selected Diseases")]
        public List<int> SelectedDiseaseIds { get; set; } = new List<int>();

        public List<MedDisease> AvailableDiseases { get; set; } = new List<MedDisease>();

        // Current medicines with detailed info for editing
        public List<OthersMedicineEdit> CurrentMedicines { get; set; } = new List<OthersMedicineEdit>();

        // Reference to patient for additional details
        public OtherPatient? Patient { get; set; }
    }

    public class OthersMedicineEdit
    {
        public int DiagnosisMedicineId { get; set; }
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BaseName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string? CompanyName { get; set; }
    }

    public class OthersDiagnosisUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool StockAdjusted { get; set; }
        public int AffectedMedicines { get; set; }
    }

}