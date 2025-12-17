using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EMS.WebApp.Models
{
    #region Common Models

    /// <summary>
    /// Common report filter parameters used across all disease trend reports
    /// </summary>
    public class DiseaseTrendFilterModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? DepartmentId { get; set; }
        public int? PlantId { get; set; }
        public int? DiseaseId { get; set; }
        public string? EmployeeType { get; set; }
        public string? FromPNo { get; set; }
        public string? ToPNo { get; set; }
    }

    /// <summary>
    /// Common report information header
    /// </summary>
    public class ReportHeaderInfo
    {
        public string PlantCode { get; set; } = string.Empty;
        public string PlantName { get; set; } = string.Empty;
        public string GeneratedOn { get; set; } = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt");
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Dropdown item for filters
    /// </summary>
    public class DropdownItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion

    #region Age Wise Report Models

    /// <summary>
    /// Disease trend analysis by age group
    /// </summary>
    public class DiseaseTrendAgeWiseViewModel
    {
        public int SlNo { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public string AgeGroup { get; set; } = string.Empty;
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
    }

    /// <summary>
    /// Age wise report response model
    /// </summary>
    public class DiseaseTrendAgeWiseReportResponse
    {
        public ReportHeaderInfo ReportInfo { get; set; } = new();
        public List<DiseaseTrendAgeWiseViewModel> Data { get; set; } = new();
        public DiseaseTrendAgeWiseSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Age wise report summary
    /// </summary>
    public class DiseaseTrendAgeWiseSummary
    {
        public int TotalRecords { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalDiseases { get; set; }
        public int TotalAgeGroups { get; set; }
    }

    #endregion

    #region Department Wise Report Models

    /// <summary>
    /// Disease trend analysis by department
    /// </summary>
    public class DiseaseTrendDeptWiseViewModel
    {
        public int SlNo { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public int DiseaseId { get; set; }
    }

    /// <summary>
    /// Department wise report response model
    /// </summary>
    public class DiseaseTrendDeptWiseReportResponse
    {
        public ReportHeaderInfo ReportInfo { get; set; } = new();
        public List<DiseaseTrendDeptWiseViewModel> Data { get; set; } = new();
        public DiseaseTrendDeptWiseSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Department wise report summary
    /// </summary>
    public class DiseaseTrendDeptWiseSummary
    {
        public int TotalRecords { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalDiseases { get; set; }
        public int TotalDepartments { get; set; }
    }

    #endregion

    #region Patient Wise Report Models

    /// <summary>
    /// Disease trend analysis by patient
    /// </summary>
    public class DiseaseTrendPatientWiseViewModel
    {
        public int SlNo { get; set; }
        public string EmpNo { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string DiseaseName { get; set; } = string.Empty;
        public string MedicineName { get; set; } = string.Empty;
        public DateTime DateTimeVisit { get; set; }
        public string DateTimeVisitFormatted => DateTimeVisit.ToString("dd/MM/yyyy HH:mm:ss");
        public string DepartmentName { get; set; } = string.Empty;
        public string? PatientType { get; set; } // Employee/Dependent/Others
        public decimal? Age { get; set; }
    }

    /// <summary>
    /// Patient wise report response model
    /// </summary>
    public class DiseaseTrendPatientWiseReportResponse
    {
        public ReportHeaderInfo ReportInfo { get; set; } = new();
        public List<DiseaseTrendPatientWiseViewModel> Data { get; set; } = new();
        public DiseaseTrendPatientWiseSummary Summary { get; set; } = new();
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    /// <summary>
    /// Patient wise report summary
    /// </summary>
    public class DiseaseTrendPatientWiseSummary
    {
        public int TotalRecords { get; set; }
        public int TotalPatients { get; set; }
        public int TotalDiseases { get; set; }
        public int TotalMedicines { get; set; }
        public int TotalVisits { get; set; }
    }

    #endregion

    #region Medicine Wise Report Models

    /// <summary>
    /// Disease trend analysis by medicine (Medicines Consumption)
    /// </summary>
    public class DiseaseTrendMedicineWiseViewModel
    {
        public int SlNo { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int QuantityUsed { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public int MedicineId { get; set; }
        public string? CompanyName { get; set; }
        public string? BaseName { get; set; }
    }

    /// <summary>
    /// Medicine wise report response model
    /// </summary>
    public class DiseaseTrendMedicineWiseReportResponse
    {
        public ReportHeaderInfo ReportInfo { get; set; } = new();
        public List<DiseaseTrendMedicineWiseViewModel> Data { get; set; } = new();
        public DiseaseTrendMedicineWiseSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Medicine wise report summary
    /// </summary>
    public class DiseaseTrendMedicineWiseSummary
    {
        public int TotalRecords { get; set; }
        public int TotalMedicines { get; set; }
        public int TotalQuantityUsed { get; set; }
        public int TotalPrescribers { get; set; }
    }

    #endregion

    #region Filter Dropdown Models

    /// <summary>
    /// All filter dropdown options for disease trend reports
    /// </summary>
    public class DiseaseTrendFilterOptions
    {
        public List<DropdownItem> Departments { get; set; } = new();
        public List<DropdownItem> Diseases { get; set; } = new();
        public List<DropdownItem> Plants { get; set; } = new();
        public List<DropdownItem> EmployeeTypes { get; set; } = new();
    }

    #endregion
}
