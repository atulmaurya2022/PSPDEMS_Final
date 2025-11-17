using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_criteria")]
    public class MedCriteria
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("criteria_id")]
        public short criteria_id { get; set; }

        [Required(ErrorMessage = "Medical Criteria Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Medical Criteria Name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Medical Criteria Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Column("criteria_name")]
        [Display(Name = "Medical Criteria Name")]
        public string criteria_name { get; set; } = null!;

        // Navigation property for related MedExamCategory records
        [JsonIgnore]
        public ICollection<MedExamCategory>? MedExamCategories { get; set; }
    }
}