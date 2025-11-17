using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("ref_med_exam_category")]
    public class RefMedExamCategory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("exam_category_id")]
        public int exam_category_id { get; set; }

        [Required]
        [StringLength(100)]
        [Column("category_name")]
        [Display(Name = "Category Name")]
        public string category_name { get; set; } = null!;

        [StringLength(20)]
        [Column("category_code")]
        [Display(Name = "Category Code")]
        public string? category_code { get; set; }

        [Column("is_active")]
        [Display(Name = "Is Active")]
        public bool is_active { get; set; } = true;

        // Navigation properties
        public ICollection<MedExaminationResult>? MedExamResults { get; set; }
    }
}