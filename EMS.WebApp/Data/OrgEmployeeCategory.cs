using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("org_employee_category")]
public partial class OrgEmployeeCategory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("emp_category_id")]
    public short emp_category_id { get; set; }

    [Required(ErrorMessage = "Employee Category Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Employee Category Name must be between 2 and 100 characters.")]
    [Display(Name = "Employee Category Name")]
    [Column("emp_category_name")]
    public string emp_category_name { get; set; } = null!;

    // Navigation property
    [JsonIgnore]
    public ICollection<HrEmployee>? HrEmployees { get; set; }
}