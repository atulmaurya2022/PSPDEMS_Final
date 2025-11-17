namespace EMS.WebApp.Data
{
    // Shared DTO for medicine data used in both Store and Compounder Indents
    public class MedicineDto
    {
        public string TempId { get; set; } = string.Empty;
        public int? IndentItemId { get; set; }
        public int MedItemId { get; set; }
        public string MedItemName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public int RaisedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? AvailableStock { get; set; }
        public bool IsNew { get; set; }
    }

    // Shared result class for medicine processing operations
    public class MedicineProcessResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasMedicines { get; set; }
    }
}