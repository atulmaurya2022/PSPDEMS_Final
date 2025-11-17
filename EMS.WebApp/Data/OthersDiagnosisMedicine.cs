using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class OthersDiagnosisMedicine
    {
        [Key]
        public int DiagnosisMedicineId { get; set; }

        public int DiagnosisId { get; set; }
        public int MedItemId { get; set; }

        public int Quantity { get; set; }

        [StringLength(50)]
        public string Dose { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Instructions { get; set; }

        // Navigation properties
        public virtual OthersDiagnosis? OthersDiagnosis { get; set; }
        public virtual MedMaster? MedMaster { get; set; }
    }
}
