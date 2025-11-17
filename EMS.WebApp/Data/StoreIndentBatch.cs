using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("store_indent_batch")]
    public class StoreIndentBatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("batch_id")]
        public int BatchId { get; set; }

        [Required]
        [Column("indent_item_id")]
        public int IndentItemId { get; set; }

        [ForeignKey("IndentItemId")]
        public virtual StoreIndentItem StoreIndentItem { get; set; }

        [Required(ErrorMessage = "Batch number is required.")]
        [StringLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_\.]+$", ErrorMessage = "Batch number can only contain letters, numbers, hyphens, underscores, and dots.")]
        [Column("batch_no")]
        public string BatchNo { get; set; }

        [Required(ErrorMessage = "Expiry date is required.")]
        [Column("expiry_date")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Expiry date cannot be in the past.")]
        [Display(Name = "Expiry Date")]
        public DateTime ExpiryDate { get; set; }

        [Required(ErrorMessage = "Received quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Received quantity must be greater than 0.")]
        [Column("received_quantity")]
        [Display(Name = "Received Quantity")]
        public int ReceivedQuantity { get; set; }

        [StringLength(50, ErrorMessage = "Vendor code cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]*$", ErrorMessage = "Vendor code can only contain letters, numbers, hyphens, and underscores.")]
        [Column("vendor_code")]
        [Display(Name = "Vendor Code")]
        public string VendorCode { get; set; }

        // Available Stock with enhanced validation
        [Column("available_stock")]
        [Display(Name = "Available Stock")]
        [Range(0, int.MaxValue, ErrorMessage = "Available stock cannot be negative.")]
        [AvailableStockValidation(ErrorMessage = "Available stock cannot exceed received quantity.")]
        public int AvailableStock { get; set; } = 0;

        public DateTime? LastDisposalDate { get; set; } 
        public string? LastDisposalBy { get; set; }
        public int TotalDisposed { get; set; } = 0;

        // Calculated property - Consumed Stock = ReceivedQuantity - AvailableStock
        [NotMapped]
        [Display(Name = "Consumed Stock")]
        public int ConsumedStock => ReceivedQuantity - AvailableStock;

        // Calculated property - Stock Status
        [NotMapped]
        [Display(Name = "Stock Status")]
        public string StockStatus
        {
            get
            {
                if (AvailableStock == 0) return "Out of Stock";
                if (AvailableStock <= (ReceivedQuantity * 0.2)) return "Low Stock";
                return "In Stock";
            }
        }
    }

    // Custom validation attribute for available stock
    public class AvailableStockValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null) return true;

            // This will be validated at the business logic level since we need access to ReceivedQuantity
            // This attribute is mainly for documentation purposes
            return true;
        }
    }

    // Custom validation attribute for received quantity vs requested quantity
    public class ReceivedQuantityValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null) return true;

            // This will be validated at the business logic level since we need access to the requested quantity
            // This attribute is mainly for documentation purposes
            return true;
        }
    }
}