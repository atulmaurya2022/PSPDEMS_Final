using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("sys_role")]
public partial class SysRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("role_id")]
    public int role_id { get; set; }

    [Required(ErrorMessage = "Role Name is required.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Role Name must be between 2 and 40 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.]+$", ErrorMessage = "Role Name can only contain letters, numbers, spaces, hyphens, underscores, and dots.")]
    [Display(Name = "Role Name")]
    [Column("role_name")]
    public string role_name { get; set; } = null!;

    [StringLength(250, ErrorMessage = "Role Description cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]+$", ErrorMessage = "Role Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Display(Name = "Role Description")]
    [Column("role_desc")]
    public string? role_desc { get; set; }

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
    public ICollection<SysUser>? SysUsers { get; set; }

    [JsonIgnore]
    public ICollection<SysAttachScreenRole>? SysAttachScreenRole { get; set; }
}