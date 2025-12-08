using EMS.WebApp.Data;
using EMS.WebApp.Services; // For MedicineStockInfo

namespace EMS.WebApp.Services
{
    public interface IOthersDiagnosisRepository
    {

        /// <summary>
        /// Gets plant code by plant ID for BCM filtering
        /// </summary>
        Task<string?> GetPlantCodeByIdAsync(int plantId);


        /// <summary>
        /// Gets all diagnosis records for listing with plant filtering
        /// </summary>
        //Task<List<OthersDiagnosisListViewModel>> GetAllDiagnosesAsync(int? userPlantId = null);
        Task<List<OthersDiagnosisListViewModel>> GetAllDiagnosesAsync(int? userPlantId = null, string? currentUser = null, bool isDoctor = false);
        /// <summary>
        /// Generates a new treatment ID
        /// </summary>
        Task<string> GenerateNewTreatmentIdAsync();

        /// <summary>
        /// Finds patient by treatment ID with plant filtering
        /// </summary>
        Task<OtherPatient?> GetPatientByTreatmentIdAsync(string treatmentId, int? userPlantId = null);

        /// <summary>
        /// Gets diseases for prescription selection with plant filtering
        /// </summary>
        Task<List<MedDisease>> GetDiseasesAsync(int? userPlantId = null);

        /// <summary>
        /// Gets all medicines for prescription selection (fallback method)
        /// </summary>
        Task<List<MedMaster>> GetMedicinesAsync();

        /// <summary>
        /// Saves diagnosis data with visit type and approval logic with plant filtering
        /// </summary>
        Task<(bool Success, string ErrorMessage)> SaveDiagnosisAsync(OthersDiagnosisViewModel model, string createdBy, int? userPlantId = null);

        /// <summary>
        /// Gets detailed diagnosis information with plant filtering
        /// </summary>
        Task<OthersDiagnosisDetailsViewModel?> GetDiagnosisDetailsAsync(int diagnosisId, int? userPlantId = null);

        /// <summary>
        /// Deletes a diagnosis record with plant filtering
        /// </summary>
        Task<bool> DeleteDiagnosisAsync(int diagnosisId, int? userPlantId = null);

        /// <summary>
        /// Gets patient details with latest diagnosis with plant filtering
        /// </summary>
        Task<OthersDiagnosisViewModel?> GetPatientForEditAsync(string treatmentId, int? userPlantId = null);

        /// <summary>
        /// Gets raw medicine data for debugging with plant filtering
        /// </summary>
        Task<object> GetRawMedicineDataAsync(int diagnosisId, int? userPlantId = null);

        /// <summary>
        /// Gets medicines from compounder indent (deprecated - use GetMedicinesFromCompounderIndentAsync)
        /// </summary>
        Task<List<MedMaster>> GetCompounderMedicinesAsync();

        // ======= NEW BATCH TRACKING AND STOCK MANAGEMENT METHODS WITH PLANT FILTERING =======

        /// <summary>
        /// Gets medicines available from compounder indent with batch information and stock, grouped by batch with plant filtering
        /// </summary>
        //Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(int? userPlantId = null);
        Task<List<MedicineStockInfo>> GetMedicinesFromCompounderIndentAsync(int? userPlantId = null, string? currentUser = null, bool isDoctor = false);
        /// <summary>
        /// Checks available stock for a specific medicine batch with plant filtering
        /// </summary>
        Task<int> GetAvailableStockAsync(int indentItemId, int? userPlantId = null);

        /// <summary>
        /// Updates available stock after prescription with plant filtering
        /// </summary>
        Task<bool> UpdateAvailableStockAsync(int indentItemId, int quantityUsed, int? userPlantId = null);

        // ======= APPROVAL METHODS WITH PLANT FILTERING =======

        /// <summary>
        /// Gets count of pending approvals with plant filtering
        /// </summary>
        Task<int> GetPendingApprovalCountAsync(int? userPlantId = null);

        /// <summary>
        /// Gets pending approval diagnoses for doctor review with plant filtering
        /// </summary>
        Task<List<OthersPendingApprovalViewModel>> GetPendingApprovalsAsync(int? userPlantId = null);

        /// <summary>
        /// Approves a diagnosis with plant filtering
        /// </summary>
        Task<bool> ApproveDiagnosisAsync(int diagnosisId, string approvedBy, int? userPlantId = null);

        /// <summary>
        /// Rejects a diagnosis with plant filtering
        /// </summary>
        Task<bool> RejectDiagnosisAsync(int diagnosisId, string rejectionReason, string rejectedBy, int? userPlantId = null);

        /// <summary>
        /// Approves multiple diagnoses with plant filtering
        /// </summary>
        Task<int> ApproveAllDiagnosesAsync(List<int> diagnosisIds, string approvedBy, int? userPlantId = null);

        // ======= NEW HELPER METHODS FOR PLANT-BASED OPERATIONS =======

        /// <summary>
        /// Gets user's plant ID based on username
        /// </summary>
        Task<int?> GetUserPlantIdAsync(string userName);

        /// <summary>
        /// Checks if user is authorized to access a specific diagnosis
        /// </summary>
        Task<bool> IsUserAuthorizedForDiagnosisAsync(int diagnosisId, int userPlantId);

        /// <summary>
        /// Checks if a diagnosis can be edited (only Pending or Rejected diagnoses)
        /// </summary>
        Task<OthersDiagnosisEditPermissionResult> CanEditDiagnosisAsync(int diagnosisId, int? userPlantId = null);

        /// <summary>
        /// Gets diagnosis data for editing with current diseases and medicines
        /// </summary>
        Task<OthersDiagnosisEditViewModel?> GetDiagnosisForEditAsync(int diagnosisId, int? userPlantId = null);

        /// <summary>
        /// Updates an existing diagnosis with new data and manages stock
        /// </summary>
        Task<OthersDiagnosisUpdateResult> UpdateDiagnosisAsync(int diagnosisId,
            List<int> selectedDiseases, List<OthersPrescriptionMedicine> medicines,
            OthersDiagnosisViewModel basicInfo, string modifiedBy, int? userPlantId = null);
    }
}