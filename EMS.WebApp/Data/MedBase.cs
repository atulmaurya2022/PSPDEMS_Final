using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_base")]
    public class MedBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("base_id")]
        [Display(Name = "Base ID")]
        public int BaseId { get; set; }

        [Required]
        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short plant_id { get; set; }

        [Required(ErrorMessage = "Base Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Base Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Base Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Column("base_name")]
        [Display(Name = "Base Name")]
        public string BaseName { get; set; } = null!;

        [StringLength(250, ErrorMessage = "Base Description cannot exceed 250 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Base Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("base_desc")]
        [Display(Name = "Base Description")]
        public string? BaseDesc { get; set; }

        [StringLength(100)]
        [Column("created_by")]
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Column("created_on")]
        [Display(Name = "Created On")]
        public DateTime? CreatedOn { get; set; }

        [StringLength(100)]
        [Column("modified_by")]
        [Display(Name = "Modified By")]
        public string? ModifiedBy { get; set; }

        [Column("modified_on")]
        [Display(Name = "Modified On")]
        public DateTime? ModifiedOn { get; set; }

        // NEW: Navigation property for plant
        [ForeignKey("plant_id")]
        public virtual OrgPlant? OrgPlant { get; set; }

        [JsonIgnore]
        public ICollection<MedMaster>? MedMasters { get; set; }
    }
}