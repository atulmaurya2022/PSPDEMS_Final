using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data
{
    [Table("locations")]
    public class Location
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("location_id")]
        public int LocationId { get; set; }

        [Column("location_name")]
        [Required]
        [StringLength(100)]
        [Display(Name = "Location Name")]
        public string LocationName { get; set; } = "";
    }
}