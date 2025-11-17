using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IHealthProfileRepository
    {
        /// <summary>
        /// Loads health-related data for an employee based on EmpNo and optional exam date with plant filtering.
        /// If examDate is null, loads the latest exam data or creates empty form for new entry.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="examDate">Optional exam date to load specific exam data.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>A ViewModel populated with health profile data.</returns>
        Task<HealthProfileViewModel> LoadFormData(int empNo, DateTime? examDate = null, int? userPlantId = null);

        /// <summary>
        /// Saves or updates all the related health profile data with plant filtering.
        /// Creates new exam header if none exists for the given date.
        /// </summary>
        /// <param name="model">The health profile view model.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task SaveFormDataAsync(HealthProfileViewModel model, int? userPlantId = null);

        /// <summary>
        /// Gets all available exam dates across all employees with plant filtering.
        /// </summary>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>List of available exam dates.</returns>
        Task<List<DateTime>> GetAvailableExamDatesAsync(int? userPlantId = null);

        /// <summary>
        /// Gets all available exam dates for a specific employee with plant filtering.
        /// </summary>
        /// <param name="empNo">The employee number.</param>
        /// <param name="userPlantId">Optional plant ID for plant-wise filtering.</param>
        /// <returns>List of available exam dates for the employee.</returns>
        Task<List<DateTime>> GetAvailableExamDatesAsync(int empNo, int? userPlantId = null);

        /// <summary>
        /// Gets matching employee IDs based on search term with plant filtering.
        /// UPDATED: Now searches by emp_id instead of emp_uid.
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
        /// Checks if user is authorized to access a specific employee's health records.
        /// </summary>
        /// <param name="empNo">Employee number.</param>
        /// <param name="userPlantId">User's plant ID.</param>
        /// <returns>True if authorized, false otherwise.</returns>
        Task<bool> IsUserAuthorizedForEmployeeAsync(int empNo, int userPlantId);
    }
}