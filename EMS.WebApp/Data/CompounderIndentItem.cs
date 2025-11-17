using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("compounder_indent_item")]
    public class CompounderIndentItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("indent_item_id")]
        public int IndentItemId { get; set; }

        [Required(ErrorMessage = "Indent ID is required.")]
        [Column("indent_id")]
        public int IndentId { get; set; }

        [Required(ErrorMessage = "Medicine selection is required.")]
        [Column("med_item_id")]
        [Display(Name = "Medicine")]
        public int MedItemId { get; set; }

        // FIXED: Removed Required attribute and updated validation
        [StringLength(50, ErrorMessage = "Vendor Code cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]*$", ErrorMessage = "Vendor Code can only contain letters, numbers, hyphens, and underscores.")]
        [Column("vendor_code")]
        [MaxLength(50)]
        [Display(Name = "Vendor Code")]
        public string? VendorCode { get; set; } // Made nullable and optional

        [Required(ErrorMessage = "Raised Quantity is required.")]
        [Column("raised_quantity")]
        [Display(Name = "Raised Quantity")]
        [Range(1, 99999, ErrorMessage = "Raised Quantity must be between 1 and 99999")]
        public int RaisedQuantity { get; set; }

        [Column("received_quantity")]
        [Display(Name = "Received Quantity")]
        [Range(0, 99999, ErrorMessage = "Received Quantity must be between 0 and 99999")]
        public int ReceivedQuantity { get; set; } = 0;

        // Calculated property - PendingQuantity = RaisedQuantity - ReceivedQuantity
        [NotMapped]
        [Display(Name = "Pending Quantity")]
        public int PendingQuantity => RaisedQuantity - ReceivedQuantity;

        [Column("unit_price")]
        [Display(Name = "Unit Price")]
        [Range(0.01, 999999.99, ErrorMessage = "Unit Price must be between 0.01 and 999999.99")]
        public decimal? UnitPrice { get; set; }

        [Column("total_amount")]
        [Display(Name = "Total Amount")]
        [Range(0.01, 9999999.99, ErrorMessage = "Total Amount must be between 0.01 and 9999999.99")]
        public decimal? TotalAmount { get; set; }

        // New fields for Compounder Inventory - Batch, Expiry, and Available Stock tracking
        [StringLength(50, ErrorMessage = "Batch No cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_\.]*$", ErrorMessage = "Batch No can only contain letters, numbers, hyphens, underscores, and dots.")]
        [Column("batch_no")]
        [MaxLength(50)]
        [Display(Name = "Batch No")]
        public string? BatchNo { get; set; }

        // ENHANCED: Added comprehensive expiry date validation
        [Column("expiry_date")]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Expiry date must be today or a future date.")]
        public DateTime? ExpiryDate { get; set; }

        [Column("available_stock")]
        [Display(Name = "Available Stock")]
        [Range(0, int.MaxValue, ErrorMessage = "Available Stock cannot be negative")]
        public int? AvailableStock { get; set; }

        [NotMapped]
        public int TotalReceivedFromStore { get; set; }

        // Navigation properties
        [ForeignKey("IndentId")]
        public virtual CompounderIndent CompounderIndent { get; set; } = null!;

        [ForeignKey("MedItemId")]
        public virtual MedMaster MedMaster { get; set; } = null!;
    }

    // Custom validation attribute for future dates
    //public class FutureDateAttribute : ValidationAttribute
    //{
    //    public override bool IsValid(object? value)
    //    {
    //        if (value == null)
    //            return true; // Allow null values (optional field)

    //        if (value is DateTime dateTime)
    //        {
    //            return dateTime.Date >= DateTime.Today;
    //        }

    //        return false;
    //    }

    //    public override string FormatErrorMessage(string name)
    //    {
    //        return $"The {name} must be today's date or a future date.";
    //    }
    //}
}