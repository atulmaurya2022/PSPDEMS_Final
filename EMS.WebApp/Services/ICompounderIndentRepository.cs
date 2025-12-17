using EMS.WebApp.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using static EMS.WebApp.Controllers.StoreIndentController;

namespace EMS.WebApp.Services
{
    public interface ICompounderIndentRepository
    {
        Task<string?> GetUserPlantCodeAsync(string userName);
        Task<string?> GetPlantCodeByIdAsync(int plantId);


        Task<CompounderIndent?> GetByIdWithItemsAsync(int id, int? userPlantId = null);
        Task<IEnumerable<CompounderIndent>> ListAsync(string currentUser = null, int? userPlantId = null, bool isDoctor = false, string? userRole = null);
        Task<IEnumerable<CompounderIndent>> ListByTypeAsync(string indentType, string currentUser = null, int? userPlantId = null, bool isDoctor = false, string? userRole = null);
        Task<IEnumerable<CompounderIndent>> ListByStatusAsync(string status, string currentUser = null, int? userPlantId = null, bool isDoctor = false, string? userRole = null);

        Task<CompounderIndent?> GetByIdAsync(int id, int? userPlantId = null);
        Task AddAsync(CompounderIndent entity);
        Task UpdateAsync(CompounderIndent entity);
        Task DeleteAsync(int id, int? userPlantId = null);

        // Medicine-related methods
        Task<IEnumerable<MedMaster>> GetMedicinesAsync(int? userPlantId = null);
        Task<MedMaster?> GetMedicineByIdAsync(int medItemId, int? userPlantId = null);

        // CompounderIndentItem methods with plant filtering
        Task AddItemAsync(CompounderIndentItem item);
        Task UpdateItemAsync(CompounderIndentItem item);
        Task DeleteItemAsync(int indentItemId, int? userPlantId = null);
        Task<CompounderIndentItem?> GetItemByIdAsync(int indentItemId, int? userPlantId = null);
        Task<IEnumerable<CompounderIndentItem>> GetItemsByIndentIdAsync(int indentId, int? userPlantId = null);

        // CompounderIndentBatch methods with plant filtering
        Task<List<CompounderIndentBatch>> GetBatchesByIndentItemIdAsync(int indentItemId, int? userPlantId = null);
        Task AddOrUpdateBatchesAsync(int indentItemId, List<CompounderIndentBatch> batches, int? userPlantId = null);
        Task DeleteBatchAsync(int indentItemId, int batchId, int? userPlantId = null);
        Task<IEnumerable<StoreIndentBatchDto>> GetAvailableStoreBatchesForMedicineAsync(int medItemId, int? userPlantId = null);

        // Business logic methods with plant filtering
        Task<bool> IsVendorCodeExistsAsync(int indentId, string vendorCode, int? excludeItemId = null, int? userPlantId = null);
        Task<bool> IsMedicineAlreadyAddedAsync(int indentId, int medItemId, int? excludeItemId = null, int? userPlantId = null);

        // NEW: Store-related methods for cross-module integration with plant filtering
        Task<int> GetTotalReceivedFromStoreAsync(int medItemId, int? userPlantId = null);
        Task<int> GetTotalAvailableStockFromStoreAsync(int medItemId, int? userPlantId = null);
        Task<StoreIndentBatch?> GetStoreBatchByBatchNoAndMedicineAsync(string batchNo, int medItemId, int? userPlantId = null);
        Task UpdateBatchAvailableStockAsync(int batchId, int newAvailableStock, int? userPlantId = null);

        // NEW: Helper methods for plant-based operations
        Task<int?> GetUserPlantIdAsync(string userName);
        Task<bool> IsUserAuthorizedForIndentAsync(int indentId, int userPlantId);

        // UPDATED: Report methods with plant filtering AND BCM compounder-wise filtering
        // Added currentUser and isDoctor parameters for BCM plant-specific access control
        Task<IEnumerable<CompounderIndentReportDto>> GetCompounderIndentReportAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? userPlantId = null,
            string currentUser = null,
            bool isDoctor = false);

        Task<IEnumerable<CompounderInventoryReportDto>> GetCompounderInventoryReportAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? userPlantId = null,
            bool showOnlyAvailable = false,
            string currentUser = null,
            bool isDoctor = false);

        // UPDATED: Added currentUser and isDoctor parameters for BCM plant-specific compounder-wise filtering
        Task<IEnumerable<DailyMedicineConsumptionReportDto>> GetDailyMedicineConsumptionReportAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? userPlantId = null,
            string currentUser = null,
            bool isDoctor = false);

        Task<IEnumerable<MedicineMasterCompounderReportDto>> GetMedicineMasterCompounderReportAsync(int? userPlantId = null);
    }

    // Updated DTOs for Reports with plant information
    public class CompounderIndentReportDto
    {
        public int IndentId { get; set; }
        public DateTime IndentDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information

    }

    public class CompounderInventoryReportDto
    {
        public int IndentId { get; set; }
        public DateTime RaisedDate { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int RaisedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public string Potency { get; set; } = string.Empty;
        public string ManufacturerBy { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string RaisedBy { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information
        public string VendorCode { get; set; } = string.Empty;
        public int AvailableStock { get; set; }
        public int ConsumedStock { get; set; }
        public DateTime? LastDisposalDate { get; set; }
        public string? LastDisposalBy { get; set; }
        public int TotalDisposed { get; set; }
        public string StockStatus { get; set; } = string.Empty;
    }

    public class DailyMedicineConsumptionReportDto
    {
        public string MedicineName { get; set; } = string.Empty;
        public int TotalStockInCompounderInventory { get; set; }
        public int IssuedQty { get; set; }
        public int ExpiredQty { get; set; }
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information

        // NEW: Total Available at Compounder Inventory = TotalStock + IssuedQty + ExpiredQty
        public int TotalAvailableAtCompounderInventory { get; set; }
    }

    public class MedicineMasterCompounderReportDto
    {
        public string MedName { get; set; } = string.Empty;
        public int TotalQtyInStore { get; set; }
        public int ExpiredQty { get; set; }
        public int ReorderLimit { get; set; }
        public string PlantName { get; set; } = string.Empty; // NEW: Plant information
    }

    public class StoreIndentBatchDto
    {
        public string BatchNo { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int AvailableStock { get; set; }
        public string VendorCode { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty; // For dropdown display

        // Enhanced properties for early expiry detection
        public int? DaysToExpiry { get; set; }
        public bool IsEarliestExpiry { get; set; }
        public string ExpiryPriority { get; set; } = "NORMAL"; // URGENT, SOON, NORMAL

        // Additional helper properties
        public string ExpiryStatus
        {
            get
            {
                if (!DaysToExpiry.HasValue) return "Unknown";

                if (DaysToExpiry <= 0) return "Expired";
                if (DaysToExpiry <= 30) return "Expires Soon";
                if (DaysToExpiry <= 90) return "Near Expiry";
                return "Good";
            }
        }

        public string ExpiryIndicator
        {
            get
            {
                if (IsEarliestExpiry) return "⭐ EARLIEST";
                if (!DaysToExpiry.HasValue) return "";

                if (DaysToExpiry <= 30) return "⚠️ EXPIRES SOON";
                if (DaysToExpiry <= 90) return "📅 NEAR EXPIRY";
                return "";
            }
        }
    }
}