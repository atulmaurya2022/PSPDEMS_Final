using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    public class OtherPatient
    {
        [Key]
        public int PatientId { get; set; }

        [StringLength(20)]
        public string TreatmentId { get; set; } = string.Empty;

        [StringLength(100)]
        public string PatientName { get; set; } = string.Empty;

        public decimal Age { get; set; } = 0;

        [StringLength(20)]
        public string PNumber { get; set; } = string.Empty;

        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [StringLength(200)]
        public string? OtherDetails { get; set; }

        
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<OthersDiagnosis> Diagnoses { get; set; } = new List<OthersDiagnosis>();

    }
}