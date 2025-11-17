using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("compounder_indent_batch")]
    public class CompounderIndentBatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("batch_id")]
        public int BatchId { get; set; }

        [Required]
        [Column("indent_item_id")]
        public int IndentItemId { get; set; }

        [ForeignKey("IndentItemId")]
        public virtual CompounderIndentItem CompounderIndentItem { get; set; }

        [Required]
        [StringLength(50)]
        [Column("batch_no")]
        public string BatchNo { get; set; }

        [Required]
        [Column("expiry_date")]
        [DataType(DataType.Date)]
        public DateTime ExpiryDate { get; set; }

        [Required]
        [Column("received_quantity")]
        public int ReceivedQuantity { get; set; }

        [StringLength(50)]
        [Column("vendor_code")]
        public string VendorCode { get; set; }

        [Column("available_stock")]
        public int AvailableStock { get; set; } = 0;

        public DateTime? LastDisposalDate { get; set; }
        public string? LastDisposalBy { get; set; }
        public int TotalDisposed { get; set; } = 0;

        [NotMapped]
        public int ConsumedStock => ReceivedQuantity - AvailableStock;

        [NotMapped]
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
}
