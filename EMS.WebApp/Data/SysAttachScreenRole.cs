using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("sys_attach_screen_role")]
public partial class SysAttachScreenRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uid")]
    public int uid { get; set; }

    [Display(Name = "Role Uid")]
    [Column("role_uid")]
    public int role_uid { get; set; }

    [Display(Name = "Screen Uid")]
    [Column("screen_uid")]
    public string screen_uid { get; set; } = ""; // stores "1,2,5"

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

    [NotMapped]
    public List<int> screen_uids
    {
        get => string.IsNullOrEmpty(screen_uid)
            ? new List<int>()
            : screen_uid.Split(',').Select(int.Parse).ToList();

        set => screen_uid = string.Join(",", value);
    }

    [ForeignKey("role_uid")]
    [JsonIgnore]
    public SysRole? SysRole { get; set; }

    [NotMapped]
    [JsonIgnore]
    public List<SysScreenName>? RelatedScreens { get; set; }
}