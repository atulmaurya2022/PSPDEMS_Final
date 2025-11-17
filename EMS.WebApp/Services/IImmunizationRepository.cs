using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IImmunizationRepository
    {
        /// <summary>
        /// Loads immunization data for an employee based on EmpNo with plant filtering.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>A ViewModel populated with immunization data.</returns>
        Task<ImmunizationViewModel> LoadFormData(int empNo, int? userPlantId = null);

        /// <summary>
        /// Saves or updates immunization record with plant filtering and duplicate prevention logic.
        /// </summary>
        /// <param name="model">The immunization view model.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="userName">User name for audit trail.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task SaveImmunizationRecordAsync(ImmunizationViewModel model, int? userPlantId = null, string? userName = null);

        /// <summary>
        /// Gets all active immunization types.
        /// </summary>
        /// <returns>List of immunization types.</returns>
        Task<List<RefImmunizationType>> GetImmunizationTypesAsync();

        /// <summary>
        /// Gets existing immunization records for an employee with plant filtering.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="immunizationTypeId">Optional immunization type ID for filtering by type.</param>
        /// <returns>List of existing immunization records.</returns>
        Task<List<MedImmunizationRecord>> GetExistingRecordsAsync(int empNo, int? userPlantId = null, int? immunizationTypeId = null);

        /// <summary>
        /// Gets matching employee IDs based on search term with plant filtering.
        /// </summary>
        /// <param name="term">Search term for employee IDs.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>List of matching employee IDs as strings.</returns>
        Task<List<string>> GetMatchingEmployeeIdsAsync(string term, int? userPlantId = null);

        /// <summary>
        /// Gets the plant ID for a specific user.
        /// </summary>
        /// <param name="userName">Username to lookup.</param>
        /// <returns>Plant ID if found, null otherwise.</returns>
        Task<int?> GetUserPlantIdAsync(string userName);

        /// <summary>
        /// Checks if user is authorized to access a specific employee's immunization records.
        /// </summary>
        /// <param name="empNo">Employee number.</param>
        /// <param name="userPlantId">User's plant ID.</param>
        /// <returns>True if authorized, false otherwise.</returns>
        Task<bool> IsUserAuthorizedForEmployeeAsync(int empNo, int userPlantId);

        /// <summary>
        /// Deletes an immunization record with authorization check.
        /// </summary>
        /// <param name="recordId">Record ID to delete.</param>
        /// <param name="userPlantId">User's plant ID for authorization.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        Task<bool> DeleteImmunizationRecordAsync(int recordId, int? userPlantId = null);

        /// <summary>
        /// Gets a specific immunization record by ID with authorization check.
        /// </summary>
        /// <param name="recordId">Record ID.</param>
        /// <param name="userPlantId">User's plant ID for authorization.</param>
        /// <returns>Immunization record if found and authorized, null otherwise.</returns>
        Task<MedImmunizationRecord?> GetImmunizationRecordAsync(int recordId, int? userPlantId = null);

        /// <summary>
        /// Finds existing immunization record by employee, immunization type, and patient name.
        /// </summary>
        /// <param name="empNo">Employee number.</param>
        /// <param name="immunizationTypeId">Immunization type ID.</param>
        /// <param name="patientName">Patient name.</param>
        /// <param name="userPlantId">User's plant ID for authorization.</param>
        /// <returns>Existing record if found, null otherwise.</returns>
        Task<MedImmunizationRecord?> FindExistingRecordAsync(int empNo, int immunizationTypeId, string patientName, int? userPlantId = null);

        /// <summary>
        /// Gets the next available dose information for a record.
        /// </summary>
        /// <param name="record">The immunization record.</param>
        /// <returns>Next dose information.</returns>
        NextDoseInfo GetNextDoseInfo(MedImmunizationRecord record);

        /// <summary>
        /// Finds existing INCOMPLETE immunization record by employee, immunization type, and patient name.
        /// </summary>
        /// <param name="empNo">Employee number.</param>
        /// <param name="immunizationTypeId">Immunization type ID.</param>
        /// <param name="patientName">Patient name.</param>
        /// <param name="userPlantId">User's plant ID for authorization.</param>
        /// <returns>Existing incomplete record if found, null otherwise.</returns>
        Task<MedImmunizationRecord?> FindIncompleteRecordAsync(int empNo, int immunizationTypeId, string patientName, int? userPlantId = null);
    }

    /// <summary>
    /// Information about the next available dose for an immunization record.
    /// </summary>
    public class NextDoseInfo
    {
        public int DoseNumber { get; set; }
        public string DoseName { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}