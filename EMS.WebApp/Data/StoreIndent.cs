using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("store_indent")]
    public class StoreIndent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("indent_id")]
        public int IndentId { get; set; }

        [Required(ErrorMessage = "Indent Type is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Indent Type must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Indent Type can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [MaxLength(50)]
        [Column("indent_type")]
        [Display(Name = "Indent Type")]
        public string IndentType { get; set; } = null!;

        [Required(ErrorMessage = "Indent Date is required.")]
        [Column("indent_date")]
        [Display(Name = "Indent Raised Date")]
        [DataType(DataType.Date)]
        public DateTime IndentDate { get; set; }

        // NEW: Plant ID field for plant-wise access control
        [Required(ErrorMessage = "Plant selection is required.")]
        [Range(1, short.MaxValue, ErrorMessage = "Please select a valid plant.")]
        [Display(Name = "Plant")]
        [Column("plant_id")]
        public short PlantId { get; set; }

        [Column("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100, ErrorMessage = "Created By cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\@]+$", ErrorMessage = "Created By contains invalid characters.")]
        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Status can only contain letters and spaces.")]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [StringLength(500, ErrorMessage = "Comments cannot exceed 500 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Comments contain invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("comments")]
        [MaxLength(500)]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [StringLength(100, ErrorMessage = "Approved By cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\@]*$", ErrorMessage = "Approved By contains invalid characters.")]
        [Column("approved_by")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        // Navigation properties
        public virtual ICollection<StoreIndentItem> StoreIndentItems { get; set; } = new List<StoreIndentItem>();

        // NEW: Navigation property for Plant
        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}