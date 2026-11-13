using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Application.DTOs.Nutrition
{
    public class WeeklyFollowUpDto
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public int WeekNumber { get; set; }
        public DateTime FollowUpDate { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BMI { get; set; }
        public decimal? BodyFatPercent { get; set; }
        public decimal? MuscleMassKg { get; set; }
        public decimal? WaistCircumferenceCm { get; set; }
        public string? BloodPressure { get; set; }
        public int? BloodPressureSys { get; set; }   // M4b: for the edit form (it parsed the text and could crash)
        public int? BloodPressureDia { get; set; }
        public string? LabResultsSummary { get; set; }
        public string? DoctorNotes { get; set; }
        public string? DietCompliance { get; set; }
        public string? SideEffects { get; set; }
        public string? NextWeekAdjustments { get; set; }
        public bool IsCompleted { get; set; }
        public List<AdministeredItemDto> AdministeredItems { get; set; } = new List<AdministeredItemDto>();
        public List<LabResultDto> LabResults { get; set; } = new List< LabResultDto > ();
    }
    public class AdministeredItemDto
    {
        public int Id { get; set; }
        public int? PackageItemId { get; set; }   // M4b: for the edit form
        public int? InjectionId { get; set; }     // M4d: items outside the package
        public int? VitaminId { get; set; }
        public string? CustomName { get; set; }
        public string? Unit { get; set; }
        public bool OutsidePackage { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal ActualQuantity { get; set; }
        public string? Notes { get; set; }
        public string? AdministeredByName { get; set; }
        public DateTime AdministeredAt { get; set; }
    }

    public class LabResultDto
    {
        public int Id { get; set; }
        public int AnalysisTypeId { get; set; }   // M4b: for the edit form
        public string AnalysisTypeName { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? ResultValue { get; set; }
        public string? ReferenceRange { get; set; }
        public bool? IsNormal { get; set; }
    }

    public class RecordFollowUpDto
    {
        public int FollowUpId { get; set; }
        public DateTime FollowUpDate { get; set; } = DateTime.Now;
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BodyFatPercent { get; set; }
        public decimal? MuscleMassKg { get; set; }
        public decimal? WaistCircumferenceCm { get; set; }
        public int? BloodPressureSys { get; set; }
        public int? BloodPressureDia { get; set; }
        public string? LabResultsSummary { get; set; }
        public string? DoctorNotes { get; set; }
        public string? DietCompliance { get; set; }
        public string? SideEffects { get; set; }
        public string? NextWeekAdjustments { get; set; }
        public List<RecordAdministeredItemDto> AdministeredItems { get; set; } = new List<RecordAdministeredItemDto>();
        public List<RecordLabResultDto> LabResults { get; set; } = new List< RecordLabResultDto > ();
        /// <summary>M4d: the patient's plan for the coming weeks, saved with the follow-up.</summary>
        public List<RecordPlannedItemDto> PlannedItems { get; set; } = new List<RecordPlannedItemDto>();
    }

    public class RecordAdministeredItemDto
    {
        /// <summary>M4d: ticked when given. Rows that aren't ticked are ignored.</summary>
        public bool Given { get; set; }
        public int? PackageItemId { get; set; }
        public int? InjectionId { get; set; }     // M4d: outside the package
        public int? VitaminId { get; set; }
        public string? CustomName { get; set; }
        public string? Unit { get; set; }
        public decimal? ActualQuantity { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>M4d: one item planned for a coming week (exactly one of the four item fields).</summary>
    public class RecordPlannedItemDto
    {
        public int WeekNumber { get; set; }
        public int? PackageItemId { get; set; }
        public int? InjectionId { get; set; }
        public int? VitaminId { get; set; }
        public string? CustomName { get; set; }
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>M4d: one line on the follow-up form or in a plan, with its name for display.</summary>
    public class FollowUpLineDto
    {
        public int WeekNumber { get; set; }
        public int? PackageItemId { get; set; }
        public int? InjectionId { get; set; }
        public int? VitaminId { get; set; }
        public string? CustomName { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;   // Injection, Vitamin or Other
        public bool OutsidePackage { get; set; }
        public bool OtherWeek { get; set; }                 // a package item planned for another week
        public bool Given { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
    }

    public record CatalogueChoiceDto(int Id, string Name, string Unit);

    /// <summary>M4d: everything the follow-up form needs.</summary>
    public class FollowUpFormDto
    {
        public int EnrollmentId { get; set; }
        public int Week { get; set; }
        public List<FollowUpLineDto> Lines { get; set; } = new();
        public List<int> PlanWeeks { get; set; } = new();
        public List<FollowUpLineDto> Plan { get; set; } = new();
        public List<FollowUpLineDto> PackageItems { get; set; } = new();   // every package item, for names
        public List<CatalogueChoiceDto> Injections { get; set; } = new();
        public List<CatalogueChoiceDto> Vitamins { get; set; } = new();
    }

    public class RecordLabResultDto
    {
        public int? AnalysisTypeId { get; set; }
        public string? ResultValue { get; set; }
        public string? ReferenceRange { get; set; }
        public bool? IsNormal { get; set; }
        public string? Notes { get; set; }
    }
}
