using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("med_exam_category")]
    public class MedExamCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("cat_id")]
        public int CatId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Category Name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Category Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Column("cat_name")]
        [Display(Name = "Category Name")]
        public string CatName { get; set; } = null!;

        [Required(ErrorMessage = "Years Frequency is required.")]
        [Range(1, 10, ErrorMessage = "Years Frequency must be between 1 and 10.")]
        [Column("years_freq")]
        [Display(Name = "Years Frequency")]
        public byte YearsFreq { get; set; }

        [Required(ErrorMessage = "Annually Rule is required.")]
        [RegularExpression(@"^(once|twice|thrice)$", ErrorMessage = "Annually Rule must be 'once', 'twice', or 'thrice'.")]
        [Column("annually_rule")]
        [Display(Name = "Annually Rule")]
        public string AnnuallyRule { get; set; } = null!;

        [Required(ErrorMessage = "Months Schedule is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "At least one month must be selected.")]
        [RegularExpression(@"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)(,(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec))*$", ErrorMessage = "Months Schedule must contain valid month abbreviations separated by commas.")]
        [Column("months_sched")]
        [Display(Name = "Months Schedule")]
        public string MonthsSched { get; set; } = null!;

        [Required(ErrorMessage = "Medical Criteria is required.")]
        [Display(Name = "Medical Criteria")]
        [Column("criteria_id")]
        public short criteria_id { get; set; }

        [StringLength(200, ErrorMessage = "Remarks cannot exceed 200 characters.")]
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

        // Navigation property for Medical Criteria
        [ForeignKey("criteria_id")]
        public MedCriteria? med_criteria { get; set; }
    }
}