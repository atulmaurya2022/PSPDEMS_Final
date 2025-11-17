using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("med_category")]
    public class MedCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("medcat_id")]
        public int MedCatId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Category Name must be between 2 and 80 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Category Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Column("medcat_name")]
        [Display(Name = "Category Name")]
        public string MedCatName { get; set; } = null!;

        [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("description")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(250, ErrorMessage = "Remarks cannot exceed 250 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Remarks contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("remarks")]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }
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
    }
}