using EMS.WebApp.Data;
namespace EMS.WebApp.Services
{
    public interface IDoctorDiagnosisRepository
    {
        Task<string?> GetUserPlantCodeAsync(string userName);
        Task<string?> GetPlantCodeByIdAsync(int plantId);

        // Employee and search methods
        Task<HrEmployee?> GetEmployeeByEmpIdAsync(string empId, int? userPlantId = null);
        Task<List<RefMedCondition>> GetMedicalConditionsAsync();
        Task<List<int>> GetEmployeeSelectedConditionsAsync(int empUid, DateTime examDate, int? userPlantId = null);
        Task<List<string>> SearchEmployeeIdsAsync(string term, int? userPlantId = null);

        // Disease and medicine methods
        Task<List<MedDisease>> GetDiseasesAsync(int? userPlantId = null);
        Task<List<MedMaster>> GetMedicinesAsync();
        //Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(int? userPlantId = null);
        Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(int? userPlantId = null, string? currentUser = null, bool isDoctor = false);
        // Stock methods
        Task<int> GetAvailableStockAsync(int indentItemId, int? userPlantId = null);
        Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed, int? userPlantId = null);

        // Prescription methods - UPDATED signatures
        Task<bool> SavePrescriptionAsync(string empId, DateTime examDate,
            List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
            VitalSigns vitalSigns, string createdBy, int? userPlantId = null,
            string? visitType = null, string? patientStatus = null, string? dependentName = null, string? userRemarks = null);

        Task<List<DiagnosisEntry>> GetEmployeeDiagnosesAsync(string empId, int? userPlantId = null);
        Task<PrescriptionDetailsViewModel?> GetPrescriptionDetailsAsync(int prescriptionId, int? userPlantId = null);
        //Task<IEnumerable<EmployeeDiagnosisListViewModel>> GetAllEmployeeDiagnosesAsync(int? userPlantId = null);
        Task<IEnumerable<EmployeeDiagnosisListViewModel>> GetAllEmployeeDiagnosesAsync(int? userPlantId = null, string? currentUser = null, bool isDoctor = false);
        Task<bool> DeletePrescriptionAsync(int prescriptionId, int? userPlantId = null, string? deletedBy = null);

        // Approval methods
        Task<int> GetPendingApprovalCountAsync(int? userPlantId = null);
        Task<List<PendingApprovalViewModel>> GetPendingApprovalsAsync(int? userPlantId = null);
        Task<bool> ApprovePrescriptionAsync(int prescriptionId, string approvedBy, int? userPlantId = null);
        Task<bool> RejectPrescriptionAsync(int prescriptionId, string rejectionReason, string rejectedBy, int? userPlantId = null);
        Task<int> ApproveAllPrescriptionsAsync(List<int> prescriptionIds, string approvedBy, int? userPlantId = null);

        // Helper methods
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForPrescriptionAsync(int prescriptionId, int userPlantId);
        Task<int?> GetEmployeePlantIdAsync(string empId);

        // Edit methods - ADD these if you're using Edit functionality
        Task<PrescriptionEditPermissionResult> CanEditPrescriptionAsync(int prescriptionId, int? userPlantId = null);
        Task<PrescriptionEditViewModel?> GetPrescriptionForEditAsync(int prescriptionId, int? userPlantId = null);
        Task<PrescriptionUpdateResult> UpdatePrescriptionAsync(int prescriptionId,
        List<int> selectedDiseases, List<PrescriptionMedicine> medicines,
        VitalSigns vitalSigns, string modifiedBy, int? userPlantId = null,
        string? visitType = null, string? patientStatus = null, string? dependentName = null); // ADD this parameter

    }


    // NEW: Vital signs helper class
    public class VitalSigns
    {
        public string? BloodPressure { get; set; }
        public string? Pulse { get; set; }
        public string? Temperature { get; set; }
    }

    // UPDATED: Class to hold medicine stock information with ID-Name-Batch format and plant filtering
    public class MedicineStockInfo
    {
        public int IndentItemId { get; set; }
        public int MedItemId { get; set; }
        public string MedItemName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int AvailableStock { get; set; }
        public string BaseName { get; set; } = "Not Defined";
        public int PlantId { get; set; } // NEW: Plant information

        // UPDATED: Display name format - ID - Name - Batch
        public string DisplayName => $"{MedItemId} - {MedItemName} - {BatchNo}";

        public string ExpiryDateFormatted => ExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A";
        public int DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now.Date).TotalDays : int.MaxValue;
    }

    public class PrescriptionEditViewModel
    {
        public int PrescriptionId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Plant { get; set; } = string.Empty;
        public DateTime PrescriptionDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }

        public string? BloodPressure { get; set; }
        public string? Pulse { get; set; }
        public string? Temperature { get; set; }
        public string? PatientStatus { get; set; }
        public string VisitType { get; set; } = "Regular Visitor";
        public string? Remarks { get; set; }

        public List<int> SelectedDiseaseIds { get; set; } = new();
        public List<MedDisease> AvailableDiseases { get; set; } = new();
        public List<PrescriptionMedicineEdit> CurrentMedicines { get; set; } = new();

        public HrEmployee? Employee { get; set; }
    }
    public class PrescriptionMedicineEdit
    {
        public int PrescriptionMedicineId { get; set; }
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BaseName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public string? CompanyName { get; set; }

        // For tracking changes
        public int? IndentItemId { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? AvailableStock { get; set; }

        public string DisplayName => !string.IsNullOrEmpty(BatchNo) ?
            $"{MedItemId} - {MedicineName} - {BatchNo}" :
            $"{MedItemId} - {MedicineName}";
    }

    // NEW: Supporting classes for edit functionality
    public class PrescriptionEditPermissionResult
    {
        public bool CanEdit { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public bool PrescriptionExists { get; set; }
        public bool IsInUserPlant { get; set; }
    }

    public class PrescriptionUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> ValidationErrors { get; set; } = new();
        public bool StockAdjusted { get; set; }
        public int AffectedMedicines { get; set; }
    }

}