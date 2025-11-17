using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    public class OthersDiagnosis
    {
        [Key]
        public int DiagnosisId { get; set; }

        public int PatientId { get; set; }

        // NEW: Plant ID field for plant-wise access control
        [Required(ErrorMessage = "Plant selection is required.")]
        [Range(1, short.MaxValue, ErrorMessage = "Please select a valid plant.")]
        [Display(Name = "Plant")]
        [Column("plant_id")]
        public short PlantId { get; set; }

        public DateTime VisitDate { get; set; }

        public DateTime? LastVisitDate { get; set; }

        [StringLength(20)]
        public string? BloodPressure { get; set; }

        [StringLength(20)]
        public string? PulseRate { get; set; }

        [StringLength(20)]
        public string? Sugar { get; set; }

        [StringLength(1000)]
        public string? Remarks { get; set; }

        [StringLength(100)]
        public string DiagnosedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        // ======= VISIT TYPE AND APPROVAL FIELDS =======

        [Column("visit_type")]
        [StringLength(50)]
        public string VisitType { get; set; } = "Regular Visitor";

        [Column("approval_status")]
        [StringLength(50)]
        public string ApprovalStatus { get; set; } = "Approved";

        [Column("approved_by")]
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        [Column("rejection_reason")]
        [StringLength(500)]
        public string? RejectionReason { get; set; }

        // Navigation properties
        public virtual OtherPatient? Patient { get; set; }
        public virtual ICollection<OthersDiagnosisDisease> DiagnosisDiseases { get; set; } = new List<OthersDiagnosisDisease>();
        public virtual ICollection<OthersDiagnosisMedicine> DiagnosisMedicines { get; set; } = new List<OthersDiagnosisMedicine>();

        // NEW: Navigation property for Plant
        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}