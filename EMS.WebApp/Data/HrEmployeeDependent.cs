using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Data;

[Table("hr_employee_dependent")]
public partial class HrEmployeeDependent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("emp_dep_id")]
    public int emp_dep_id { get; set; }

    [Required]
    [Column("plant_id")]
    [Display(Name = "Plant")]
    public short plant_id { get; set; }

    [Required(ErrorMessage = "Employee is required.")]
    [Display(Name = "Employee")]
    [Column("emp_uid")]
    public int emp_uid { get; set; }

    [Required(ErrorMessage = "Dependent Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Dependent Name must be between 2 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z\s\-\.]+$", ErrorMessage = "Dependent Name can only contain letters, spaces, hyphens, and dots.")]
    [Display(Name = "Dependent Name")]
    [Column("dep_name")]
    public string dep_name { get; set; } = null!;

    [Required(ErrorMessage = "Dependent DOB is required.")]
    [Display(Name = "Date of Birth")]
    [Column("dep_dob")]
    [DependentDateOfBirthValidation]
    public DateOnly? dep_dob { get; set; }

    [Required(ErrorMessage = "Relation is required.")]
    //[StringLength(50, MinimumLength = 2, ErrorMessage = "Relation must be between 2 and 50 characters.")]
    [AllowedDependentRelation]
    [Display(Name = "Relation")]
    [Column("relation")]
    public string relation { get; set; } = null!;

    [Required(ErrorMessage = "Gender is required.")]
    [RegularExpression(@"^[MFO]$", ErrorMessage = "Gender must be M (Male), F (Female), or O (Other).")]
    [Display(Name = "Gender")]
    [Column("gender")]
    public string gender { get; set; } = null!;

    [Display(Name = "Status")]
    [Column("is_active")]
    public bool is_active { get; set; }

    [Display(Name = "Marital Status")]
    [Column("marital_status")]
    public bool? marital_status { get; set; }

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

    [ForeignKey("emp_uid")]
    public HrEmployee? HrEmployee { get; set; }

    // NEW: Navigation property for plant
    [ForeignKey("plant_id")]
    public virtual OrgPlant? OrgPlant { get; set; }

    // Helper property to calculate age
    [NotMapped]
    public int? Age
    {
        get
        {
            if (!dep_dob.HasValue) return null;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - dep_dob.Value.Year;
            if (dep_dob.Value > today.AddYears(-age)) age--;
            return age;
        }
    }

    // Helper property to check if child is over age limit
    [NotMapped]
    public bool IsChildOverAgeLimit
    {
        get
        {
            return relation?.ToLower() == "child" && Age.HasValue && Age.Value > 21;
        }
    }
}

// Custom validation attributes
public class AllowedDependentRelationAttribute : ValidationAttribute
{
    private readonly string[] _allowedRelations = { "Wife", "Husband", "Child" };

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult("Relation is required.");
        }

        var relation = value.ToString();

        // Check if relation is in allowed list
        if (!_allowedRelations.Contains(relation, StringComparer.OrdinalIgnoreCase))
        {
            return new ValidationResult("Only Wife, Husband, and Child relations are allowed. Parents are not permitted as dependents.");
        }

        return ValidationResult.Success;
    }
}

public class DependentDateOfBirthValidationAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var dependent = (HrEmployeeDependent)validationContext.ObjectInstance;

        if (value is DateOnly dobValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Check if date is in future
            if (dobValue > today)
            {
                return new ValidationResult("Date of Birth cannot be in the future.");
            }

            // Calculate age
            var age = today.Year - dobValue.Year;
            if (dobValue > today.AddYears(-age)) age--;

            // Check maximum age limit
            if (age > 100)
            {
                return new ValidationResult("Age cannot exceed 100 years.");
            }

            // Check child age limit
            if (dependent.relation?.ToLower() == "child" && age > 21)
            {
                return new ValidationResult("Child dependents cannot be older than 21 years.");
            }
        }

        return ValidationResult.Success;
    }
}