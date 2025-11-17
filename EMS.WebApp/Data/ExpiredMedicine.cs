using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("expired_medicine")]
    public class ExpiredMedicine
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("expired_medicine_id")]
        public int ExpiredMedicineId { get; set; }

        // CORRECTED: Make nullable since Store medicines won't have this
        [Column("compounder_indent_item_id")]
        [Display(Name = "Source Compounder Indent Item")]
        public int? CompounderIndentItemId { get; set; }

        // Store Indent Item support
        [Column("store_indent_item_id")]
        [Display(Name = "Source Store Indent Item")]
        public int? StoreIndentItemId { get; set; }

        // Source type to distinguish between Compounder and Store
        [Required]
        [Column("source_type")]
        [MaxLength(20)]
        [Display(Name = "Source Type")]
        public string SourceType { get; set; } = "Compounder"; // "Compounder" or "Store"

        // FIXED: Make PlantId nullable to handle NULL values in database
        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        [Required]
        [Column("medicine_name")]
        [MaxLength(120)]
        [Display(Name = "Medicine Name")]
        public string MedicineName { get; set; } = null!;

        [Column("company_name")]
        [MaxLength(120)]
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Required]
        [Column("batch_number")]
        [MaxLength(50)]
        [Display(Name = "Batch Number")]
        public string BatchNumber { get; set; } = null!;

        [Required]
        [Column("vendor_code")]
        [MaxLength(50)]
        [Display(Name = "Vendor Code")]
        public string VendorCode { get; set; } = null!;

        [Required]
        [Column("expiry_date")]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        // FIXED: Make QuantityExpired nullable to handle NULL values
        [Column("quantity_expired")]
        [Display(Name = "Quantity Expired")]
        public int? QuantityExpired { get; set; }

        // FIXED: Make IndentId nullable to handle NULL values
        [Column("indent_id")]
        [Display(Name = "Source Indent ID")]
        public int? IndentId { get; set; }

        [Column("indent_number")]
        [MaxLength(50)]
        [Display(Name = "Indent Number")]
        public string? IndentNumber { get; set; }

        [Column("unit_price")]
        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public decimal? UnitPrice { get; set; }

        [Column("total_value")]
        [Display(Name = "Total Value")]
        [DataType(DataType.Currency)]
        public decimal? TotalValue { get; set; }

        [Column("detected_date")]
        [Display(Name = "Detected Date")]
        public DateTime DetectedDate { get; set; } = DateTime.Now;

        [Column("detected_by")]
        [MaxLength(100)]
        [Display(Name = "Detected By")]
        public string? DetectedBy { get; set; }

        [Column("status")]
        [MaxLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending Disposal"; // Pending Disposal, Issued to Biomedical Waste

        [Column("biomedical_waste_issued_date")]
        [Display(Name = "Biomedical Waste Issued Date")]
        public DateTime? BiomedicalWasteIssuedDate { get; set; }

        [Column("biomedical_waste_issued_by")]
        [MaxLength(100)]
        [Display(Name = "Biomedical Waste Issued By")]
        public string? BiomedicalWasteIssuedBy { get; set; }

        [Column("type_of_medicine")]
        [MaxLength(30)]
        [Display(Name = "Type of Medicine")]
        public string TypeOfMedicine { get; set; } = "Select Type of Medicine"; // Default value: Select Type of Medicine, Solid, Liquid, Gel

        // FIXED: Updated calculated properties to handle nullable values
        [NotMapped]
        [Display(Name = "Days Overdue")]
        public int DaysOverdue => (DateTime.Today - ExpiryDate.Date).Days;

        [NotMapped]
        [Display(Name = "Is Critical")]
        public bool IsCritical => DaysOverdue > 90; // Critical if expired for more than 90 days

        [NotMapped]
        [Display(Name = "Priority Level")]
        public string PriorityLevel
        {
            get
            {
                if (DaysOverdue <= 30) return "Low";
                if (DaysOverdue <= 90) return "Medium";
                return "High";
            }
        }

        [Column("batch_id")]
        public int? BatchId { get; set; }

        [NotMapped]
        [Display(Name = "Is Disposed")]
        public bool IsDisposed => Status == "Issued to Biomedical Waste";

        // Display source with icon
        [NotMapped]
        [Display(Name = "Source")]
        public string SourceDisplay => SourceType == "Store" ? "🏪 Store" : "🧪 Compounder";

        [NotMapped]
        [Display(Name = "Source Badge Class")]
        public string SourceBadgeClass => SourceType == "Store" ? "bg-primary" : "bg-info";

        // Navigation properties
        [ForeignKey("CompounderIndentItemId")]
        public virtual CompounderIndentItem? CompounderIndentItem { get; set; }

        // Navigation property for Store Indent Item
        [ForeignKey("StoreIndentItemId")]
        public virtual StoreIndentItem? StoreIndentItem { get; set; }

        [ForeignKey("BatchId")]
        public virtual CompounderIndentBatch? CompounderIndentBatch { get; set; }

        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }

        // Static method to get medicine types
        [NotMapped]
        public static List<string> MedicineTypes => new List<string> { "Select Type of Medicine", "Solid", "Liquid", "Gel" };

        // Method to get badge class for medicine type
        [NotMapped]
        public string TypeBadgeClass
        {
            get
            {
                return TypeOfMedicine?.ToLower() switch
                {
                    "liquid" => "bg-info",
                    "solid" => "bg-success",
                    "gel" => "bg-warning text-dark",
                    "select type of medicine" => "bg-secondary",
                    _ => "bg-secondary"
                };
            }
        }

        // Helper method to get the correct navigation property
        [NotMapped]
        public object? SourceIndentItem => SourceType == "Store" ? StoreIndentItem : CompounderIndentItem;

        // Helper method to get medicine details from correct source
        [NotMapped]
        public MedMaster? MedMaster => SourceType == "Store" ? StoreIndentItem?.MedMaster : CompounderIndentItem?.MedMaster;
    }
}