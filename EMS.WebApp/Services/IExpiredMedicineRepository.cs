using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public interface IExpiredMedicineRepository
    {
        /// <summary>
        /// Gets plant code by plant ID for BCM filtering
        /// </summary>
        Task<string?> GetPlantCodeByIdAsync(int plantId);

        //Task<ExpiredMedicine?> GetByIdAsync(int id, int? userPlantId = null, string? userRole = null);
        Task<ExpiredMedicine?> GetByIdAsync(int id, int? userPlantId = null, string? userRole = null, string? currentUser = null);
        Task<ExpiredMedicine?> GetByIdWithDetailsAsync(int id, int? userPlantId = null, string? userRole = null);
        //Task<IEnumerable<ExpiredMedicine>> ListAsync(int? userPlantId = null, string? userRole = null);
        //Task<IEnumerable<ExpiredMedicine>> ListPendingDisposalAsync(int? userPlantId = null, string? userRole = null);
        //Task<IEnumerable<ExpiredMedicine>> ListDisposedAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null, string? userRole = null);

        Task<IEnumerable<ExpiredMedicine>> ListAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null);
        Task<IEnumerable<ExpiredMedicine>> ListPendingDisposalAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null);
        Task<IEnumerable<ExpiredMedicine>> ListDisposedAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null, string? userRole = null, string? currentUser = null);

        Task AddAsync(ExpiredMedicine entity);
        Task UpdateAsync(ExpiredMedicine entity);
        Task DeleteAsync(int id, int? userPlantId = null, string? userRole = null);

        // Business logic methods with plant filtering and role-based access
        Task<IEnumerable<ExpiredMedicine>> GetByStatusAsync(string status, int? userPlantId = null, string? userRole = null);
        Task<IEnumerable<ExpiredMedicine>> GetByPriorityLevelAsync(string priority, int? userPlantId = null, string? userRole = null);
        Task<IEnumerable<ExpiredMedicine>> GetCriticalExpiredMedicinesAsync(int? userPlantId = null, string? userRole = null);

        // UPDATED: Check if already tracked with source type support
        Task<bool> IsAlreadyTrackedAsync(int? compounderIndentItemId = null, int? storeIndentItemId = null, string? batchNo = null, DateTime? expiryDate = null, int? userPlantId = null);
        Task<bool> IsCompounderItemAlreadyTrackedAsync(int compounderIndentItemId, string batchNo, DateTime expiryDate, int? userPlantId = null);
        Task<bool> IsStoreItemAlreadyTrackedAsync(int storeIndentItemId, string batchNo, DateTime expiryDate, int? userPlantId = null);

        // Biomedical waste operations with plant filtering and role-based access
        Task IssueToBiomedicalWasteAsync(int expiredMedicineId, string issuedBy, int? userPlantId = null, string? userRole = null, string? remarks = null);
        Task BulkIssueToBiomedicalWasteAsync(List<int> expiredMedicineIds, string issuedBy, int? userPlantId = null, string? userRole = null, string? remarks = null);

        // UPDATED: Sync operations to detect newly expired medicines from both sources
        Task<List<ExpiredMedicine>> DetectNewExpiredMedicinesAsync(string detectedBy, int? userPlantId = null, string? sourceType = null);
        Task<List<ExpiredMedicine>> DetectNewExpiredCompounderMedicinesAsync(string detectedBy, int? userPlantId = null);
        Task<List<ExpiredMedicine>> DetectNewExpiredStoreMedicinesAsync(string detectedBy, int? userPlantId = null);
        Task SyncExpiredMedicinesAsync(string detectedBy, int? userPlantId = null, string? sourceType = null);

        // Statistics and reporting with plant filtering and role-based access
        //Task<int> GetTotalExpiredCountAsync(int? userPlantId = null, string? userRole = null);
        Task<int> GetTotalExpiredCountAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null);
        //Task<int> GetPendingDisposalCountAsync(int? userPlantId = null, string? userRole = null);
        Task<int> GetPendingDisposalCountAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null);
        //Task<int> GetDisposedCountAsync(int? userPlantId = null, string? userRole = null);
        Task<int> GetDisposedCountAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null);
        Task<decimal> GetTotalExpiredValueAsync(int? userPlantId = null, string? userRole = null);
        Task<IEnumerable<ExpiredMedicine>> GetExpiredMedicinesForPrintAsync(List<int> ids, int? userPlantId = null, string? userRole = null);

        // Inline editing methods with plant filtering and role-based access
        Task UpdateMedicineTypeAsync(int expiredMedicineId, string typeOfMedicine, int? userPlantId = null, string? userRole = null);

        // Helper methods for plant-based operations and role-based access
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<string?> GetUserRoleAsync(string userName);
        Task<bool> IsUserAuthorizedForExpiredMedicineAsync(int expiredMedicineId, int userPlantId, string? userRole = null);

        // NEW: Source type validation methods
        Task<bool> CanUserAccessSourceTypeAsync(string sourceType, string? userRole);
        Task<List<string>> GetAccessibleSourceTypesAsync(string? userRole);

        // NEW: Role-based statistics
        //Task<Dictionary<string, int>> GetStatisticsBySourceTypeAsync(int? userPlantId = null, string? userRole = null);
        Task<Dictionary<string, int>> GetStatisticsBySourceTypeAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null);
    }
}




