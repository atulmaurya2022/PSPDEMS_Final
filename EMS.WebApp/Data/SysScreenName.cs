using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("sys_screen_name")]
public partial class SysScreenName
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("screen_uid")]
    public int screen_uid { get; set; }

    [Required(ErrorMessage = "Screen Name is required.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Screen Name must be between 2 and 40 characters.")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9]*$", ErrorMessage = "Screen Name must start with a letter and contain only letters and numbers.")]
    [Display(Name = "Screen Name")]
    [Column("screen_name")]
    public string screen_name { get; set; } = null!;

    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
    [Display(Name = "Description")]
    [Column("screen_description")]
    public string? screen_description { get; set; }

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

    // Remove the collection navigation property since we can't have a direct relationship
    // public ICollection<SysAttachScreenRole>? SysAttachScreenRole { get; set; }
}