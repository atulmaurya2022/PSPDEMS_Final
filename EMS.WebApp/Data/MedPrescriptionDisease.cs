using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    public class MedPrescriptionDisease
    {
        [Key]
        public int PrescriptionDiseaseId { get; set; }

        [Required(ErrorMessage = "Prescription ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid prescription ID.")]
        public int PrescriptionId { get; set; }

        [Required(ErrorMessage = "Disease ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid disease ID.")]
        public int DiseaseId { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [StringLength(100, ErrorMessage = "Created by cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\@]*$", ErrorMessage = "Created by contains invalid characters.")]
        public string? CreatedBy { get; set; }

        // Navigation properties
        [ForeignKey("PrescriptionId")]
        public virtual MedPrescription? MedPrescription { get; set; }

        [ForeignKey("DiseaseId")]
        public virtual MedDisease? MedDisease { get; set; }

        // NEW: Validation method
        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = new List<string>();

            if (PrescriptionId <= 0)
                validationErrors.Add("Invalid prescription ID.");

            if (DiseaseId <= 0)
                validationErrors.Add("Invalid disease ID.");

            return validationErrors.Count == 0;
        }
    }
}