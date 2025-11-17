using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("store_indent_item")]
    public class StoreIndentItem
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

        //[Required(ErrorMessage = "Vendor Code is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Vendor Code must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Vendor Code can only contain letters, numbers, hyphens, and underscores.")]
        [Column("vendor_code")]
        [MaxLength(50)]
        [Display(Name = "Vendor Code")]
        public string? VendorCode { get; set; } 

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

        [StringLength(500, ErrorMessage = "Remark cannot exceed 500 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Remark contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("remark")]
        [MaxLength(500)]
        [Display(Name = "Remark")]
        public string? Remark { get; set; }
        // New fields for Store Inventory with enhanced validation
        [StringLength(50, ErrorMessage = "Batch No cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_\.]*$", ErrorMessage = "Batch No can only contain letters, numbers, hyphens, underscores, and dots.")]
        [Column("batch_no")]
        [MaxLength(50)]
        [Display(Name = "Batch No")]
        public string? BatchNo { get; set; }

        [Column("expiry_date")]
        [Display(Name = "Expiry Date")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Expiry date cannot be in the past.")]
        public DateTime? ExpiryDate { get; set; }

        // Navigation properties
        [ForeignKey("IndentId")]
        public virtual StoreIndent StoreIndent { get; set; } = null!;

        [ForeignKey("MedItemId")]
        public virtual MedMaster MedMaster { get; set; } = null!;
    }

    // Custom validation attribute for future dates
    public class FutureDateAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null) return true; // Allow null values

            if (value is DateTime dateValue)
            {
                return dateValue.Date >= DateTime.Today;
            }

            return false;
        }
    }
}