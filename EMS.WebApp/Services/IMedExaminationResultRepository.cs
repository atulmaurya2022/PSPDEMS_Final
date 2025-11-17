using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IMedExaminationResultRepository
    {
        /// <summary>
        /// Loads medical examination result data for an employee with plant filtering.
        /// If resultId is null, creates empty form for new entry.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="resultId">Optional result ID to load specific result data.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>A ViewModel populated with medical examination result data.</returns>
        Task<MedExaminationResultViewModel> LoadFormData(int empNo, int? resultId = null, int? userPlantId = null);

        /// <summary>
        /// Saves or updates medical examination result data with plant filtering.
        /// Creates new result record if none exists.
        /// </summary>
        /// <param name="model">The medical examination result view model.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="username">Username of the creator.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task SaveFormDataAsync(MedExaminationResultViewModel model, int? userPlantId = null, string? username = null);

        /// <summary>
        /// Updates medical examination result data with plant filtering and permission checks.
        /// </summary>
        /// <param name="model">The medical examination result view model.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="username">Username of the user attempting to update.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task UpdateFormDataAsync(MedExaminationResultViewModel model, int? userPlantId = null, string? username = null);

        /// <summary>
        /// Gets all available medical examination results for a specific employee with plant filtering.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>List of medical examination results for the employee.</returns>
        Task<List<MedExaminationResult>> GetEmployeeExamResultsAsync(int empNo, int? userPlantId = null);

        /// <summary>
        /// Gets a paginated and filtered list of medical examination results with approval status.
        /// </summary>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="searchTerm">Optional search term for filtering.</param>
        /// <param name="currentUsername">Current username for permission checks.</param>
        /// <returns>List of medical examination result items.</returns>
        Task<List<MedExaminationResultListItemViewModel>> GetResultsListAsync(int? userPlantId = null, string searchTerm = "", string? currentUsername = null);

        /// <summary>
        /// Gets medical examination result for viewing with approval information.
        /// </summary>
        /// <param name="resultId">Result ID.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="currentUsername">Current username for permission checks.</param>
        /// <returns>Medical examination result view model.</returns>
        Task<MedExaminationResultViewModel?> GetResultForViewAsync(int resultId, int? userPlantId = null, string? currentUsername = null);

        /// <summary>
        /// Gets medical examination result for editing with approval information and permission checks.
        /// </summary>
        /// <param name="resultId">Result ID.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <param name="currentUsername">Current username for permission checks.</param>
        /// <returns>Medical examination result view model.</returns>
        Task<MedExaminationResultViewModel?> GetResultForEditAsync(int resultId, int? userPlantId = null, string? currentUsername = null);

        /// <summary>
        /// Checks if user can edit a specific result.
        /// User can edit only if they are the creator and the result is not approved.
        /// </summary>
        /// <param name="resultId">Result ID.</param>
        /// <param name="username">Username to check.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>True if user can edit, false otherwise.</returns>
        Task<bool> CanUserEditResultAsync(int resultId, string username, int? userPlantId = null);

        /// <summary>
        /// Checks if user can delete a specific result.
        /// User can delete only if they are the creator and the result is not approved.
        /// </summary>
        /// <param name="resultId">Result ID.</param>
        /// <param name="username">Username to check.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>True if user can delete, false otherwise.</returns>
        Task<bool> CanUserDeleteResultAsync(int resultId, string username, int? userPlantId = null);

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
        /// Gets matching employee IDs based on search term with plant filtering.
        /// Searches by emp_id.
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
        /// Checks if user is authorized to access a specific employee's medical examination results.
        /// </summary>
        /// <param name="empNo">Employee number.</param>
        /// <param name="userPlantId">User's plant ID.</param>
        /// <returns>True if authorized, false otherwise.</returns>
        Task<bool> IsUserAuthorizedForEmployeeAsync(int empNo, int userPlantId);

        /// <summary>
        /// Deletes a medical examination result record with plant filtering and permission checks.
        /// </summary>
        /// <param name="resultId">Result ID to delete.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        Task<bool> DeleteExamResultAsync(int resultId, int? userPlantId = null);

        /// <summary>
        /// Gets medical examination result by ID with plant filtering.
        /// </summary>
        /// <param name="resultId">Result ID.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>Medical examination result if found, null otherwise.</returns>
        Task<MedExaminationResult?> GetExamResultByIdAsync(int resultId, int? userPlantId = null);
    }
}