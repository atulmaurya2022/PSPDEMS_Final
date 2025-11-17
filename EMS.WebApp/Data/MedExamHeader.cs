using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_exam_header")]
    public class MedExamHeader
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("exam_id")]
        public int exam_id { get; set; }

        [Column("emp_uid")]
        public int emp_uid { get; set; }

        [Column("exam_date")]
        [Display(Name = "Exam Date")]
        public DateOnly? exam_date { get; set; }

        [Column("food_habit")]
        public string? food_habit { get; set; }

        // NEW: Plant ID field for plant-wise access control
        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        [ForeignKey("emp_uid")]
        public HrEmployee? HrEmployee { get; set; }

        public ICollection<MedWorkHistory>? MedWorkHistories { get; set; }
        public ICollection<MedExamWorkArea>? MedExamWorkAreas { get; set; }
        public ICollection<MedExamCondition>? MedExamConditions { get; set; }
        public ICollection<MedGeneralExam> MedGeneralExams { get; set; } = new List<MedGeneralExam>();

        // NEW: Navigation property for Plant
        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}