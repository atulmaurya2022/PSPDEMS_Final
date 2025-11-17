using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data
{
    public class OthersDiagnosisDisease
    {
        [Key]
        public int DiagnosisDiseaseId { get; set; }

        public int DiagnosisId { get; set; }
        public int DiseaseId { get; set; }

        // Navigation properties
        public virtual OthersDiagnosis? OthersDiagnosis { get; set; }
        public virtual MedDisease? MedDisease { get; set; }
    }

}
