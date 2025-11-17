using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using EMS.WebApp.Services;

namespace EMS.WebApp.Data
{
    public class ImmunizationViewModel
    {
        public int EmpNo { get; set; }

        [Display(Name = "Immunization Type")]
        [Required(ErrorMessage = "Please select an immunization type")]
        public int? ImmunizationTypeId { get; set; }

        [Display(Name = "Name of Patient")]
        [Required(ErrorMessage = "Please select a patient")]
        public string? PatientName { get; set; }

        [Display(Name = "Relationship")]
        public string? Relationship { get; set; }

        [Display(Name = "1st Dose")]
        [DataType(DataType.Date)]
        public DateTime? Dose1Date { get; set; }

        [Display(Name = "2nd Dose")]
        [DataType(DataType.Date)]
        public DateTime? Dose2Date { get; set; }

        [Display(Name = "3rd Dose")]
        [DataType(DataType.Date)]
        public DateTime? Dose3Date { get; set; }

        [Display(Name = "4th Dose")]
        [DataType(DataType.Date)]
        public DateTime? Dose4Date { get; set; }

        [Display(Name = "5th Dose")]
        [DataType(DataType.Date)]
        public DateTime? Dose5Date { get; set; }

        [Display(Name = "Booster Dose")]
        [DataType(DataType.Date)]
        public DateTime? BoosterDoseDate { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Plant")]
        public short? PlantId { get; set; }

        // Flag to indicate if this is a new entry
        public bool IsNewEntry { get; set; }

        // Existing record ID for updates
        public int? RecordId { get; set; }

        // Next dose information
        [BindNever]
        public NextDoseInfo? NextDoseInfo { get; set; }

        // Flag to indicate if updating existing record for same combination
        [BindNever]
        public bool IsUpdatingExistingRecord { get; set; }

        // Reference Data
        public List<RefImmunizationType> ImmunizationTypes { get; set; } = new List<RefImmunizationType>();

        [BindNever]
        public List<HrEmployeeDependent> Dependents { get; set; } = new List<HrEmployeeDependent>();

        [BindNever]
        public List<HrEmployee> EmployeeDetails { get; set; } = new List<HrEmployee>();

        [BindNever]
        public List<MedImmunizationRecord> ExistingRecords { get; set; } = new List<MedImmunizationRecord>();

        // Navigation property for Plant (read-only)
        [BindNever]
        public OrgPlant? OrgPlant { get; set; }

        // Helper property for plant name display
        [BindNever]
        public string PlantName => OrgPlant?.plant_name ?? "Unknown Plant";

        // Helper property to get patient options (employee + dependents)
        [BindNever]
        public List<PatientOption> PatientOptions
        {
            get
            {
                var options = new List<PatientOption>();

                // Add "Self" as first option for employee
                foreach (var emp in EmployeeDetails)
                {
                    options.Add(new PatientOption
                    {
                        Name = emp.emp_name,
                        Relationship = "Self",
                        IsSelf = true
                    });
                }

                // Add dependents as patients
                foreach (var dependent in Dependents)
                {
                    options.Add(new PatientOption
                    {
                        Name = dependent.dep_name,
                        Relationship = dependent.relation,
                        IsSelf = false
                    });
                }

                return options;
            }
        }

        // Helper property to determine next available dose field (legacy - replaced by NextDoseInfo)
        [BindNever]
        public string NextAvailableDose
        {
            get
            {
                if (!Dose1Date.HasValue) return "dose1";
                if (!Dose2Date.HasValue) return "dose2";
                if (!Dose3Date.HasValue) return "dose3";
                if (!Dose4Date.HasValue) return "dose4";
                if (!Dose5Date.HasValue) return "dose5";
                if (!BoosterDoseDate.HasValue) return "booster";
                return "complete";
            }
        }

        // Helper property to get current dose progress text
        [BindNever]
        public string DoseProgressText
        {
            get
            {
                if (NextDoseInfo == null) return "";

                if (NextDoseInfo.IsComplete)
                    return "All doses completed";

                return $"Next: {NextDoseInfo.DoseName}";
            }
        }

        // Helper method to check if a specific dose field should be enabled
        public bool IsDoseFieldEnabled(int doseNumber)
        {
            if (NextDoseInfo == null) return doseNumber == 1; // For new records, only 1st dose

            if (NextDoseInfo.IsComplete) return false; // No more doses needed

            return NextDoseInfo.DoseNumber == doseNumber; // Only current dose is enabled
        }
    }

    public class PatientOption
    {
        public string Name { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public bool IsSelf { get; set; } = false;
    }
}