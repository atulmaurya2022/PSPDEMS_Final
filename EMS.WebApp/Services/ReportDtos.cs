namespace EMS.WebApp.Services
{
    public sealed class NearExpiryReportRowDto
    {
        public string Scope { get; set; } = string.Empty; // "Store" or "Compounder"
        public int BatchId { get; set; }
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public System.DateTime ExpiryDate { get; set; }
        public int DaysFromPivot { get; set; } // (ExpiryDate - Pivot).Days
        public int AvailableStock { get; set; }
        public string VendorCode { get; set; } = string.Empty;
    }

    public sealed class StoreIssueRowDto
    {
        public int IndentId { get; set; }
        public System.DateTime IndentDate { get; set; }
        public string Compounder { get; set; } = string.Empty; // CreatedBy (compounder user)
        public string PlantName { get; set; } = string.Empty;
        public int MedItemId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int RaisedQty { get; set; }
        public int IssuedQty { get; set; }           // = ReceivedQuantity
        public int BalanceQty { get; set; }          // Raised - Issued
    }
}
