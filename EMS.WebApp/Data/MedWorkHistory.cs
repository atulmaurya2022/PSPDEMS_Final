using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_work_history")]

    public class MedWorkHistory

    {

        [Key]

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

        [Column("work_uid")]

        public int work_uid { get; set; }

        [Column("emp_uid")]

        [Required]

        public int emp_uid { get; set; }

        [Column("exam_id")]

        [Required]

        public int exam_id { get; set; }

        [Column("job_name")]

        [StringLength(200)]

        [Display(Name = "Job Name")]

        public string? job_name { get; set; }

        [Column("years_in_job")]

        [Display(Name = "Years in Job")]

        public decimal? years_in_job { get; set; }

        [Column("work_env")]

        [StringLength(200)]

        [Display(Name = "Work Environment")]

        public string? work_env { get; set; }

        [Column("ppe")]

        [StringLength(200)]

        [Display(Name = "PPE")]

        public string? ppe { get; set; }

        [Column("job_injuries")]

        [StringLength(200)]

        [Display(Name = "Job Related Injuries")]

        public string? job_injuries { get; set; }

        [ForeignKey("emp_uid")]

        public HrEmployee? HrEmployee { get; set; }

        [ForeignKey("exam_id")]

        public MedExamHeader? MedExamHeader { get; set; }

    }
}
 