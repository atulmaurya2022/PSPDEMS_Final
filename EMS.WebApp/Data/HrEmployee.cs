using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EMS.WebApp.Data;

[Table("hr_employee")]
public partial class HrEmployee
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("emp_uid")]
    public int emp_uid { get; set; }

    [Required(ErrorMessage = "Employee ID is required.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Employee ID must be between 2 and 20 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Employee ID can only contain letters, numbers, hyphens, and underscores.")]
    [Display(Name = "Employee ID")]
    [Column("emp_id")]
    public string emp_id { get; set; } = null!;

    [Required(ErrorMessage = "Employee Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Employee Name must be between 2 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z\s\-\.]+$", ErrorMessage = "Employee Name can only contain letters, spaces, hyphens, and dots.")]
    [Display(Name = "Employee Name")]
    [Column("emp_name")]
    public string emp_name { get; set; } = null!;

    [Display(Name = "Date of Birth")]
    [Column("emp_DOB")]
    public DateOnly? emp_DOB { get; set; }

    [Required(ErrorMessage = "Gender is required.")]
    [RegularExpression(@"^[MFO]$", ErrorMessage = "Gender must be M (Male), F (Female), or O (Other).")]
    [Display(Name = "Gender")]
    [Column("emp_Gender")]
    public string emp_Gender { get; set; } = null!;

    [Required(ErrorMessage = "Grade is required.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "Grade must be between 1 and 10 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Grade can only contain letters, numbers, hyphens, and underscores.")]
    [Display(Name = "Grade")]
    [Column("emp_Grade")]
    public string emp_Grade { get; set; } = null!;

    [Required(ErrorMessage = "Department is required.")]
    [Display(Name = "Department")]
    [Column("dept_id")]
    public short dept_id { get; set; }

    [Required(ErrorMessage = "Plant is required.")]
    [Display(Name = "Plant")]
    [Column("plant_id")]
    public short plant_id { get; set; }

    [Required(ErrorMessage = "Employee Category is required.")]
    [Display(Name = "Employee Category")]
    [Column("emp_category_id")]
    public short emp_category_id { get; set; }

    [StringLength(5, ErrorMessage = "Blood Group cannot exceed 5 characters.")]
    [RegularExpression(@"^(A\+|A-|B\+|B-|AB\+|AB-|O\+|O-)?$", ErrorMessage = "Blood Group must be a valid blood type (A+, A-, B+, B-, AB+, AB-, O+, O-).")]
    [Display(Name = "Blood Group")]
    [Column("emp_blood_Group")]
    public string? emp_blood_Group { get; set; }

    [Display(Name = "Marital Status")]
    [Column("marital_status")]
    public bool? marital_status { get; set; } = false;

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

    [ForeignKey("dept_id")]
    public OrgDepartment? org_department { get; set; }

    [ForeignKey("plant_id")]
    public OrgPlant? org_plant { get; set; }

    [ForeignKey("emp_category_id")]
    public OrgEmployeeCategory? org_employee_category { get; set; }

    [JsonIgnore]
    public ICollection<HrEmployeeDependent>? HrEmployeeDependents { get; set; }

    [JsonIgnore]
    public ICollection<MedExamHeader>? MedExamHeaders { get; set; }
    public ICollection<MedWorkHistory>? MedWorkHistories { get; set; }

    public ICollection<MedGeneralExam>? MedGeneralExams { get; set; }
}