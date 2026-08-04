using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data;

[Table("ref_dependent_relation")]
public partial class RefDependentRelation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("relation_id")]
    public int relation_id { get; set; }

    [Column("plant_id")]
    public short? plant_id { get; set; }

    [Required]
    [StringLength(50)]
    [Column("relation_name")]
    public string relation_name { get; set; } = null!;

    [Column("is_active")]
    public bool is_active { get; set; } = true;

    [ForeignKey("plant_id")]
    public virtual OrgPlant? OrgPlant { get; set; }
}
