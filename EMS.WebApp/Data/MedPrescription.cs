using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    public class MedPrescription
    {
        [Key]
        public int PrescriptionId { get; set; }

        public int emp_uid { get; set; }
        public int exam_id { get; set; }
        public string? DependentName { get; set; } = "Self";
        public DateTime PrescriptionDate { get; set; }

        [StringLength(20)]
        public string? BloodPressure { get; set; }

        [StringLength(20)]
        public string? Pulse { get; set; }

        [StringLength(20)]
        public string? Temperature { get; set; }

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Remarks { get; set; }

        // UPDATED: Patient Status field - now nullable with no default value, not required
        [StringLength(50)]
        [Display(Name = "Patient Status")]
        public string? PatientStatus { get; set; }

        // NEW: Plant ID field for plant-wise access control
        [Required(ErrorMessage = "Plant selection is required.")]
        [Range(1, short.MaxValue, ErrorMessage = "Please select a valid plant.")]
        [Display(Name = "Plant")]
        [Column("plant_id")]
        public short PlantId { get; set; }

        // ======= APPROVAL FIELDS =======
        [StringLength(50)]
        public string ApprovalStatus { get; set; } = "Approved"; // Default to Approved for regular visits

        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        // Navigation properties
        public virtual HrEmployee? HrEmployee { get; set; }
        public virtual MedExamHeader? MedExamHeader { get; set; }
        public virtual ICollection<MedPrescriptionDisease> PrescriptionDiseases { get; set; } = new List<MedPrescriptionDisease>();
        public virtual ICollection<MedPrescriptionMedicine> PrescriptionMedicines { get; set; } = new List<MedPrescriptionMedicine>();

        // NEW: Navigation property for Plant
        [ForeignKey("PlantId")]
        public virtual OrgPlant? OrgPlant { get; set; }
    }
}