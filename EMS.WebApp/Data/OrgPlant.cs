using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

public partial class OrgPlant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public short plant_id { get; set; }

    [Required(ErrorMessage = "Plant Code is required.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Plant Code must be between 2 and 20 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Plant Code can only contain letters, numbers, hyphens, and underscores.")]
    [Display(Name = "Plant Code")]
    [Column("plant_code")]
    public string plant_code { get; set; } = null!;

    [Required(ErrorMessage = "Plant Name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Plant Name must be between 2 and 120 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Plant Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
    [Display(Name = "Plant Name")]
    [Column("plant_name")]
    public string plant_name { get; set; } = null!;

    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Display(Name = "Description")]
    [Column("Description")]
    public string? Description { get; set; }

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

    [JsonIgnore]
    public ICollection<HrEmployee>? HrEmployees { get; set; }
    //[JsonIgnore]
    //public ICollection<SysUser>? SysUsers { get; set; }
}