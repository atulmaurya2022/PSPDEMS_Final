using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("med_immunization_record")]
    public class MedImmunizationRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("immun_record_uid")]
        public int immun_record_uid { get; set; }

        [Column("emp_uid")]
        [Required]
        public int emp_uid { get; set; }

        [Column("immun_type_uid")]
        [Required]
        public int immun_type_uid { get; set; }

        [Column("patient_name")]
        [MaxLength(100)]
        [Display(Name = "Name of Patient")]
        public string? patient_name { get; set; }

        [Column("relationship")]
        [MaxLength(50)]
        [Display(Name = "Relationship")]
        public string? relationship { get; set; }

        [Column("dose_1_date")]
        [Display(Name = "1st Dose")]
        [DataType(DataType.Date)]
        public DateOnly? dose_1_date { get; set; }

        [Column("dose_2_date")]
        [Display(Name = "2nd Dose")]
        [DataType(DataType.Date)]
        public DateOnly? dose_2_date { get; set; }

        [Column("dose_3_date")]
        [Display(Name = "3rd Dose")]
        [DataType(DataType.Date)]
        public DateOnly? dose_3_date { get; set; }

        [Column("dose_4_date")]
        [Display(Name = "4th Dose")]
        [DataType(DataType.Date)]
        public DateOnly? dose_4_date { get; set; }

        [Column("dose_5_date")]
        [Display(Name = "5th Dose")]
        [DataType(DataType.Date)]
        public DateOnly? dose_5_date { get; set; }

        [Column("booster_dose_date")]
        [Display(Name = "Booster Dose")]
        [DataType(DataType.Date)]
        public DateOnly? booster_dose_date { get; set; }

        [Column("remarks")]
        [MaxLength(500)]
        [Display(Name = "Remarks")]
        public string? remarks { get; set; }

        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short? plant_id { get; set; }

        [Column("created_date")]
        public DateTime created_date { get; set; } = DateTime.Now;

        [Column("updated_date")]
        public DateTime updated_date { get; set; } = DateTime.Now;

        [Column("created_by")]
        [MaxLength(50)]
        public string? created_by { get; set; }

        [Column("updated_by")]
        [MaxLength(50)]
        public string? updated_by { get; set; }

        // Navigation properties
        [ForeignKey("emp_uid")]
        public virtual HrEmployee? HrEmployee { get; set; }

        [ForeignKey("immun_type_uid")]
        public virtual RefImmunizationType? RefImmunizationType { get; set; }

        [ForeignKey("plant_id")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}