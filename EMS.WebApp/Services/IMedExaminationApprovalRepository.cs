using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedExaminationApprovalRepository
    {
        /// <summary>
        /// Gets filtered medical examination approval data with plant filtering.
        /// </summary>
        /// <param name="categoryId">Optional category filter.</param>
        /// <param name="locationId">Optional test location filter.</param>
        /// <param name="approvalStatus">Optional approval status filter.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>A ViewModel populated with filtered approval data.</returns>
        Task<MedExaminationApprovalViewModel> GetApprovalDataAsync(
            int? categoryId = null,
            int? locationId = null,
            string? approvalStatus = "Pending",
            int? userPlantId = null);

        /// <summary>
        /// Approves selected medical examination results.
        /// </summary>
        /// <param name="approvalIds">List of approval IDs to approve.</param>
        /// <param name="approvedBy">Username of approver.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>Number of records approved.</returns>
        Task<int> ApproveExaminationsAsync(List<int> approvalIds, string approvedBy, int? userPlantId = null);

        /// <summary>
        /// Rejects selected medical examination results.
        /// </summary>
        /// <param name="approvalIds">List of approval IDs to reject.</param>
        /// <param name="rejectedBy">Username of rejector.</param>
        /// <param name="rejectionReason">Reason for rejection.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>Number of records rejected.</returns>
        Task<int> RejectExaminationsAsync(List<int> approvalIds, string rejectedBy, string? rejectionReason = null, int? userPlantId = null);

        /// <summary>
        /// Un-approves selected medical examination results.
        /// </summary>
        /// <param name="approvalIds">List of approval IDs to un-approve.</param>
        /// <param name="unApprovedBy">Username of un-approver.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>Number of records un-approved.</returns>
        Task<int> UnApproveExaminationsAsync(List<int> approvalIds, string unApprovedBy, int? userPlantId = null);

        /// <summary>
        /// Gets all medical examination categories.
        /// </summary>
        /// <returns>List of medical examination categories.</returns>
        Task<List<MedExamCategory>> GetExamCategoriesAsync();

        /// <summary>
        /// Gets all active test locations.
        /// </summary>
        /// <returns>List of active test locations.</returns>
        Task<List<Location>> GetTestLocationsAsync();

        /// <summary>
        /// Gets the plant ID for a specific user.
        /// </summary>
        /// <param name="userName">Username to lookup.</param>
        /// <returns>Plant ID if found, null otherwise.</returns>
        Task<int?> GetUserPlantIdAsync(string userName);

        /// <summary>
        /// Creates approval records for examination results that don't have them yet.
        /// This should be called when new examination results are created.
        /// </summary>
        /// <param name="resultId">Medical examination result ID.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>True if approval record created successfully.</returns>
        Task<bool> CreateApprovalRecordAsync(int resultId, int? userPlantId = null);

        /// <summary>
        /// Gets approval statistics for dashboard/reporting.
        /// </summary>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>Approval statistics.</returns>
        Task<ApprovalStatistics> GetApprovalStatisticsAsync(int? userPlantId = null);
    }

    public class ApprovalStatistics
    {
        public int TotalPending { get; set; }
        public int TotalApproved { get; set; }
        public int TotalRejected { get; set; }
        public int TotalUnApproved { get; set; }
        public int Total => TotalPending + TotalApproved + TotalRejected + TotalUnApproved;
    }
}