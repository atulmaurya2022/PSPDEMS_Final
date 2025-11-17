using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;


namespace EMS.WebApp.Data
{
    [Table("ref_med_condition")]

    public class RefMedCondition

    {

        [Key]

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

        [Column("cond_uid")]

        public int cond_uid { get; set; }

        [Column("cond_code")]

        [StringLength(40)]

        [Display(Name = "Condition Code")]

        public string? cond_code { get; set; }

        [Column("cond_desc")]

        [StringLength(40)]

        [Display(Name = "Condition Description")]

        public string? cond_desc { get; set; }

        // Navigation properties


        public ICollection<MedExamCondition> MedExamConditions { get; set; } = new List<MedExamCondition>();

    }
}

 