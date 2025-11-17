using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMS.WebApp.Data;

[Table("med_ambulance_master")]
public partial class MedAmbulanceMaster
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("amb_id")]
    public int amb_id { get; set; }

    [Required(ErrorMessage = "Vehicle Number is required.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Vehicle Number must be between 3 and 20 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Vehicle Number can only contain letters, numbers, hyphens, and underscores.")]
    [Display(Name = "Vehicle Number")]
    [Column("vehicle_no")]
    public string vehicle_no { get; set; } = null!;

    [Required(ErrorMessage = "Provider is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Provider must be between 2 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Provider can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
    [Display(Name = "Provider")]
    [Column("provider")]
    public string provider { get; set; } = null!;

    [StringLength(50, ErrorMessage = "Vehicle Type cannot exceed 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]*$", ErrorMessage = "Vehicle Type can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
    [Display(Name = "Vehicle Type")]
    [Column("vehicle_type")]
    public string? vehicle_type { get; set; }

    [Required(ErrorMessage = "Max Capacity is required.")]
    [Range(1, 50, ErrorMessage = "Max Capacity must be between 1 and 50.")]
    [Display(Name = "Max Capacity")]
    [Column("max_capacity")]
    public byte? max_capacity { get; set; }

    [Required(ErrorMessage = "Active Status is required.")]
    [Display(Name = "Is Active")]
    [Column("is_active")]
    public bool? is_active { get; set; }
    [StringLength(100)]
    [Column("created_by")]
    [Display(Name = "Created By")]
    public string? CreatedBy { get; set; }

    [Column("created_on")]
    [Display(Name = "Created On")]
    public DateTime? CreatedOn { get; set; }

    [StringLength(100)]
    [Column("modified_by")]
    [Display(Name = "Modified By")]
    public string? ModifiedBy { get; set; }

    [Column("modified_on")]
    [Display(Name = "Modified On")]
    public DateTime? ModifiedOn { get; set; }
}