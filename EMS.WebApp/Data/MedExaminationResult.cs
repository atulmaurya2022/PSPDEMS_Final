using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_examination_result")]
    public class MedExaminationResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("result_id")]
        public int ResultId { get; set; }

        [Column("emp_uid")]
        [Required]
        public int EmpUid { get; set; }

        [Column("cat_id")]
        [Required]
        [Display(Name = "Medical Examination Category")]
        public int CatId { get; set; }

        [Column("last_checkup_date")]
        [Display(Name = "Last Check Up Date")]
        public DateOnly? LastCheckupDate { get; set; }

        [Column("test_date")]
        [Display(Name = "Test Date")]
        public DateOnly? TestDate { get; set; }

        [Column("location_id")]
        [Display(Name = "Test Location")]
        public int? LocationId { get; set; }

        [Column("result")]
        [StringLength(50)]
        [Display(Name = "Result")]
        public string? Result { get; set; }

        [Column("remarks")]
        [StringLength(2000)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Plant ID for plant-wise access control
        [Column("plant_id")]
        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

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

        // Navigation properties
        [ForeignKey("EmpUid")]
        public virtual HrEmployee? HrEmployee { get; set; }

        [ForeignKey("CatId")]
        public virtual MedExamCategory? MedExamCategory { get; set; }

        [ForeignKey("LocationId")]
        public virtual Location? Location { get; set; }

        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}