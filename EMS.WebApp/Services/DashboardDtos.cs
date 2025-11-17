
using System;

namespace EMS.WebApp.Services
{
    // DTO returned to Doctor dashboard summary widget
    public sealed class DoctorDashboardDto
    {
        public int PendingStoreIndentApprovals { get; set; }
        public int PendingCompounderIndentApprovals { get; set; }
        public int PendingPrescriptionApprovals { get; set; }
        public int ExpiredMedicinesPendingDisposal { get; set; }
        public int NearExpiryMedicineCount { get; set; }
        public int NearExpiryDays { get; set; }
    }

    public sealed class NearExpiryDto
    {
        public int MedicineId { get; set; }
        public int BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int AvailableStock { get; set; }
        public string VendorCode { get; set; } = string.Empty;
    }

    public sealed class ExpiredMedicineDto
    {
        public int Id { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public string TypeOfMedicine { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiredOn { get; set; }
    }

    public sealed class StoreIndentPendingDto
    {
        public int IndentId { get; set; }
        public string IndentType { get; set; } = string.Empty;
        public DateTime IndentDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public sealed class CompounderIndentPendingDto
    {
        public int IndentId { get; set; }
        public string IndentType { get; set; } = string.Empty;
        public DateTime IndentDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public sealed class PrescriptionPendingDto
    {
        public int PrescriptionId { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Plant { get; set; } = string.Empty;
        public DateTime PrescriptionDate { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public int MedicineCount { get; set; }
    }


    // ===== Store Dashboard DTOs =====
    public sealed class StoreDashboardDto
    {
        public int PendingIndents { get; set; }
        public int ApprovedAwaitingReceipt { get; set; }
        public int MyDraftIndents { get; set; }
        public int NearExpiryBatches { get; set; }
        public int ExpiredBatches { get; set; }
        public int ExpiredMedicinesPendingDisposal { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int NearExpiryDays { get; set; }
    }

    public sealed class StoreLowStockDto
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int TotalAvailable { get; set; }
        public int? ReorderLevel { get; set; }
    }

    // ===== Compounder Dashboard DTOs =====
    public sealed class CompounderDashboardDto
    {
        public int PendingIndents { get; set; }
        public int ApprovedAwaitingReceipt { get; set; }
        public int MyDraftIndents { get; set; }
        public int NearExpiryBatches { get; set; }
        public int ExpiredBatches { get; set; }
        public int ExpiredMedicinesPendingDisposal { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int NearExpiryDays { get; set; }
    }

    public sealed class CompounderLowStockDto
    {
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int TotalAvailable { get; set; }
        public int? ReorderLevel { get; set; }
    }

}
