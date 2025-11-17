using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data
{
    [Table("med_examination_approval")]
    public class MedExaminationApproval
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("approval_id")]
        public int ApprovalId { get; set; }

        [Column("result_id")]
        [Required]
        public int ResultId { get; set; }

        [Column("emp_uid")]
        [Required]
        public int EmpUid { get; set; }

        [Column("cat_id")]
        [Required]
        public int CatId { get; set; }

        [Column("approval_status")]
        [StringLength(20)]
        [Display(Name = "Approval Status")]
        public string? ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected, UnApproved

        [Column("approved_by")]
        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Column("approved_on")]
        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }

        [Column("rejection_reason")]
        [StringLength(500)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

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
        [ForeignKey("ResultId")]
        public virtual MedExaminationResult? MedExaminationResult { get; set; }

        [ForeignKey("EmpUid")]
        public virtual HrEmployee? HrEmployee { get; set; }

        [ForeignKey("CatId")]
        public virtual MedExamCategory? MedExamCategory { get; set; }

        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}