using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;


namespace EMS.WebApp.Data
{
    [Table("med_exam_condition")]

    public class MedExamCondition

    {

        [Key]

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

        [Column("exam_condition_uid")]

        public int exam_condition_uid { get; set; }

        [Column("exam_id")]

        [Required]

        public int exam_id { get; set; }

        [Column("cond_uid")]

        [Required]

        public int cond_uid { get; set; }

        [Column("present")]

        [Display(Name = "Present")]

        public bool? present { get; set; }

        [ForeignKey("exam_id")]

        public MedExamHeader? MedExamHeader { get; set; }

        [ForeignKey("cond_uid")]

        public RefMedCondition? RefMedCondition { get; set; }

    }

}