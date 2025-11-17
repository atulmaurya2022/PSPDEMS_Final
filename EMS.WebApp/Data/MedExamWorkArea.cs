using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_exam_work_area")]
    public class MedExamWorkArea
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("work_area_uid")]
        public int work_area_uid { get; set; }

        [Column("exam_id")]
        [Required]
        public int exam_id { get; set; }

        [Column("area_uid")]
        [Required]
        public int area_uid { get; set; }

        [ForeignKey("exam_id")]
        public MedExamHeader? MedExamHeader { get; set; }

        [ForeignKey("area_uid")]
        public RefWorkArea? RefWorkArea { get; set; }
    }
}