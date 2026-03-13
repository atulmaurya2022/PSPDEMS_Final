using EMS.WebApp.Data.Migrations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    public class MedPrescriptionMedicine
    {
        [Key]
        public int PrescriptionMedicineId { get; set; }

        public int PrescriptionId { get; set; }
        public int MedItemId { get; set; }

        public int Quantity { get; set; }

        [StringLength(50)]
        public string Dose { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Instructions { get; set; }
        // NEW: Track which specific batch was deducted
        public int? BatchId { get; set; }
        // Navigation properties
        public virtual MedPrescription? MedPrescription { get; set; }
        public virtual MedMaster? MedMaster { get; set; }

        [ForeignKey("BatchId")]
        public virtual CompounderIndentBatch? CompounderIndentBatch { get; set; }
    }
}