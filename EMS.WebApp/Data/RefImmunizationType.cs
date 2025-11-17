using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("ref_immunization_type")]
    public class RefImmunizationType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("immun_type_uid")]
        public int immun_type_uid { get; set; }

        [Column("immun_type_name")]
        [Required]
        [MaxLength(100)]
        [Display(Name = "Immunization Type")]
        public string immun_type_name { get; set; } = string.Empty;

        // Navigation property
        public virtual ICollection<MedImmunizationRecord>? MedImmunizationRecords { get; set; }
    }
}