using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_general_exam")]
    public class MedGeneralExam
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("general_exam_uid")]
        public int general_exam_uid { get; set; }

        [Column("emp_uid")]
        [Required]
        public int emp_uid { get; set; }

        [Column("exam_id")]
        [Required]
        public int exam_id { get; set; }

        [Column("height_cm")]
        [Display(Name = "Height (cm)")]
        public short? height_cm { get; set; }

        [Column("weight_kg")]
        [Display(Name = "Weight (kg)")]
        public short? weight_kg { get; set; }

        [Column("abdomen")]
        [StringLength(40)]
        [Display(Name = "Abdomen")]
        public string? abdomen { get; set; }

        [Column("pulse")]
        [StringLength(20)]
        [Display(Name = "Pulse")]
        public string? pulse { get; set; }

        [Column("bp")]
        [StringLength(20)]
        [Display(Name = "BP (mmHg)")]
        public string? bp { get; set; }

        [Column("bmi")]
        [Display(Name = "BMI")]
        public decimal? bmi { get; set; }

        [Column("ent")]
        [StringLength(100)]
        [Display(Name = "ENT")]
        public string? ent { get; set; }

        [Column("rr")]
        [StringLength(100)]
        [Display(Name = "RR")]
        public string? rr { get; set; }

        [Column("opthal")]
        [StringLength(100)]
        [Display(Name = "Ophthal")]
        public string? opthal { get; set; }

        [Column("cvs")]
        [StringLength(100)]
        [Display(Name = "CVS")]
        public string? cvs { get; set; }

        [Column("skin")]
        [StringLength(100)]
        [Display(Name = "Skin")]
        public string? skin { get; set; }

        [Column("cns")]
        [StringLength(100)]
        [Display(Name = "CNS")]
        public string? cns { get; set; }

        [Column("genito_urinary")]
        [StringLength(100)]
        [Display(Name = "Genito Urinary")]
        public string? genito_urinary { get; set; }

        [Column("respiratory")]
        [StringLength(100)]
        [Display(Name = "Respiratory")]
        public string? respiratory { get; set; }

        [Column("others")]
        [StringLength(200)]
        [Display(Name = "Others")]
        public string? others { get; set; }

        [Column("remarks")]
        [StringLength(2000)]
        [Display(Name = "Remarks")]
        public string? remarks { get; set; }

        // NEW: Plant ID field for plant-wise access control
        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        [ForeignKey("emp_uid")]
        public HrEmployee? HrEmployee { get; set; }

        [ForeignKey("exam_id")]
        public MedExamHeader? MedExamHeader { get; set; }

        // NEW: Navigation property for Plant
        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}
