using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    [Table("med_diagnosis")]
    public class MedDiagnosis
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "Diagnosis ID")]
        [Column("diag_id")]
        public int diag_id { get; set; }

        [Required]
        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short plant_id { get; set; }

        [Required(ErrorMessage = "Diagnosis Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Diagnosis Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Diagnosis Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Display(Name = "Diagnosis Name")]
        [Column("diag_name")]
        public string diag_name { get; set; } = null!;

        [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Display(Name = "Description")]
        [Column("diag_desc")]
        public string? diag_desc { get; set; }

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
    }
}