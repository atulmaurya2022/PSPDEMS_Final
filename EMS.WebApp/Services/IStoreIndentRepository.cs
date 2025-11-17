using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public interface IStoreIndentRepository
    {
        // Updated methods with plant filtering
        Task<StoreIndent?> GetByIdWithItemsAsync(int id, int? userPlantId = null);
        Task<IEnumerable<StoreIndent>> ListAsync(string currentUser = null, int? userPlantId = null);
        Task<IEnumerable<StoreIndent>> ListByTypeAsync(string indentType, string currentUser = null, int? userPlantId = null);
        Task<IEnumerable<StoreIndent>> ListByStatusAsync(string status, string currentUser = null, int? userPlantId = null);
        Task<StoreIndent?> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(StoreIndent entity);
        Task UpdateAsync(StoreIndent entity);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Medicine-related methods
        Task<IEnumerable<MedMaster>> GetMedicinesAsync(int? userPlantId = null);
        Task<MedMaster?> GetMedicineByIdAsync(int medItemId, int? userPlantId = null);

        // StoreIndentItem methods
        Task AddItemAsync(StoreIndentItem item);
        Task UpdateItemAsync(StoreIndentItem item);
        Task DeleteItemAsync(int indentItemId, int? userPlantId = null);
        Task<StoreIndentItem?> GetItemByIdAsync(int indentItemId, int? userPlantId = null);
        Task<IEnumerable<StoreIndentItem>> GetItemsByIndentIdAsync(int indentId, int? userPlantId = null);

        // Business logic methods
        Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null, int? userPlantId = null);

        // StoreIndentBatch methods with plant filtering
        Task<List<StoreIndentBatch>> GetBatchesByIndentItemIdAsync(int indentItemId, int? userPlantId = null);
        Task AddOrUpdateBatchesAsync(int indentItemId, List<StoreIndentBatch> batches, int? userPlantId = null);
        Task DeleteBatchAsync(int batchId, int? userPlantId = null);
        Task<int> GetTotalReceivedFromStoreAsync(int medItemId, int? userPlantId = null);
        Task<int> GetTotalAvailableStockFromStoreAsync(int medItemId, int? userPlantId = null);
        Task<StoreIndentBatch?> GetStoreBatchByBatchNoAndMedicineAsync(string batchNo, int medItemId, int? userPlantId = null);
        Task UpdateBatchAvailableStockAsync(int batchId, int newAvailableStock, int? userPlantId = null);

        // Updated Report methods with plant filtering
        Task<IEnumerable<StoreIndentBatchReportDto>> GetStoreIndentBatchReportAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null);
        Task<IEnumerable<StoreInventoryBatchReportDto>> GetStoreInventoryBatchReportAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null);
        Task<IEnumerable<MedicineMasterStoreReportDto>> GetMedicineMasterStoreReportAsync(int? userPlantId = null);

        // New helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForIndentAsync(int indentId, int userPlantId);
        


    }

    public class StoreIndentBatchReportDto
    {
        public int IndentId { get; set; }
        public DateTime IndentDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public int RaisedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information
    }

    public class StoreInventoryBatchReportDto
    {
        public int IndentId { get; set; }
        public DateTime RaisedDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int RaisedQuantity { get; set; }
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerBy { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public int ReceivedQuantity { get; set; }
        public int AvailableStock { get; set; }
        public int ConsumedStock { get; set; }
        public DateTime? LastDisposalDate { get; set; }
        public string? LastDisposalBy { get; set; }
        public int TotalDisposed { get; set; } 
        public DateTime? ExpiryDate { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
        public string StockStatus { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information
    }

    public class MedicineMasterStoreReportDto
    {
        public string MedName { get; set; } = string.Empty;
        public int TotalQtyInStore { get; set; }
        public int ExpiredQty { get; set; }
        public int ReorderLimit { get; set; }
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information
    }
}