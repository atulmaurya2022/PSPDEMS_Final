using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace EMS.WebApp.Data
{
    [Table("med_ref_hospital")]
    public class MedRefHospital
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("hosp_id")]
        public int hosp_id { get; set; }

        [Required(ErrorMessage = "Hospital Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Hospital Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Hospital Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Display(Name = "Hospital Name")]
        [Column("hosp_name")]
        public string hosp_name { get; set; }

        [Required(ErrorMessage = "Hospital Code is required.")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Hospital Code must be between 2 and 20 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Hospital Code can only contain letters, numbers, hyphens, and underscores.")]
        [Display(Name = "Hospital Code")]
        [Column("hosp_code")]
        public string hosp_code { get; set; }

        [Required(ErrorMessage = "Speciality is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Speciality must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\(\)\[\]]+$", ErrorMessage = "Speciality contains invalid characters.")]
        [Display(Name = "Speciality")]
        [Column("speciality")]
        public string speciality { get; set; }

        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}#/]*$", ErrorMessage = "Address contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Display(Name = "Address")]
        [Column("address")]
        public string address { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\,\;\:\!\?\(\)\[\]\{\}]*$", ErrorMessage = "Description contains invalid characters. Special characters like <, >, &, \", ', script tags are not allowed.")]
        [Column("description")]
        public string description { get; set; }

        [Required(ErrorMessage = "Tax Category is required.")]
        [RegularExpression(@"^[TN]$", ErrorMessage = "Tax Category must be either T (Taxable) or N (Non-Taxable).")]
        [Display(Name = "Tax Category")]
        [Column("tax_category")]
        public string tax_category { get; set; } // T = Taxable, N = Non-Taxable

        [Required(ErrorMessage = "Vendor Name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Vendor Name must be between 2 and 120 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]+$", ErrorMessage = "Vendor Name can only contain letters, numbers, spaces, hyphens, underscores, dots, parentheses, and brackets.")]
        [Display(Name = "Vendor Name")]
        [Column("vendor_name")]
        public string vendor_name { get; set; }

        [Required(ErrorMessage = "Vendor Code is required.")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Vendor Code must be between 2 and 20 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Vendor Code can only contain letters, numbers, hyphens, and underscores.")]
        [Display(Name = "Vendor Code")]
        [Column("vendor_code")]
        public string vendor_code { get; set; }

        [StringLength(100, ErrorMessage = "Contact Person Name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s\-\.]*$", ErrorMessage = "Contact Person Name can only contain letters, spaces, hyphens, and dots.")]
        [Column("contact_person_name")]
        public string contact_person_name { get; set; }

        [StringLength(100, ErrorMessage = "Contact Person Email cannot exceed 100 characters.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
        [Column("contact_person_email_id")]
        public string contact_person_email_id { get; set; }

        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile Number must be exactly 10 digits.")]
        [Column("mobile_number_1")]
        public long? mobile_number_1 { get; set; }

        [RegularExpression(@"^[0-9]{10}$", ErrorMessage = "Mobile Number must be exactly 10 digits.")]
        [Column("mobile_number_2")]
        public long? mobile_number_2 { get; set; }

        [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Phone Number must be 10-11 digits.")]
        [Column("phone_number_1")]
        public long? phone_number_1 { get; set; }

        [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Phone Number must be 10-11 digits.")]
        [Column("phone_number_2")]
        public long? phone_number_2 { get; set; }
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
}