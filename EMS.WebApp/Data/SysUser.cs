using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("sys_user")]
public partial class SysUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("user_id")]
    public int user_id { get; set; }

    [Required(ErrorMessage = "ADID  is required.")]
    [StringLength(15, MinimumLength = 2, ErrorMessage = "ADID must be between 2 and 15 characters.")]
    [RegularExpression(@"^[pPiI0-9_]+$", ErrorMessage = "Only P, I, underscore, and digits are allowed.")]
    [Display(Name = "Active Directory ID")]
    [Column("adid")]
    public string? adid { get; set; }

    [Required(ErrorMessage = "Role selection is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid role.")]
    [Display(Name = "Role")]
    [Column("role_id")]
    public int role_id { get; set; }

    [Required(ErrorMessage = "Plant selection is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a valid plant.")]
    [Display(Name = "Plant")]
    [Column("plant_id")]
    public short plant_id { get; set; }

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 80 characters.")]
    [RegularExpression(@"^[a-zA-Z\s\.\-']+$", ErrorMessage = "Full Name can only contain letters, spaces, dots, hyphens, and apostrophes.")]
    [Display(Name = "Full Name")]
    [Column("full_name")]
    public string full_name { get; set; } = null!;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(120, ErrorMessage = "Email cannot exceed 120 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address format.")]
    [Display(Name = "Email Address")]
    [Column("email")]
    public string email { get; set; } = null!;

    [Required(ErrorMessage = "User Status is required.")]
    [Display(Name = "User Status")]
    [Column("is_active")]
    public bool is_active { get; set; }

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

    [ForeignKey("role_id")]
    public SysRole? SysRole { get; set; }

    [ForeignKey("plant_id")]
    public OrgPlant? OrgPlant { get; set; }

    //this is for blocking multiple tab
    public string? SessionToken { get; set; }
    public DateTime? TokenIssuedAt { get; set; }
    public DateTime? LastActivityTime { get; set; }
}