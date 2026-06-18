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
        public decimal? BMI { get; set; }
        public decimal? BodyFatPercent { get; set; }
        public decimal? MuscleMassKg { get; set; }
        public decimal? WaistCircumferenceCm { get; set; }
        public string? BloodPressure { get; set; }
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
        public string ItemName { get; set; } = string.Empty;
        public decimal ActualQuantity { get; set; }
        public string? AdministeredByName { get; set; }
        public DateTime AdministeredAt { get; set; }
    }

    public class LabResultDto
    {
        public int Id { get; set; }
        public string AnalysisTypeName { get; set; } = string.Empty;
        public string? ResultValue { get; set; }
        public string? ReferenceRange { get; set; }
        public bool? IsNormal { get; set; }
    }

    public class RecordFollowUpDto
    {
        public int FollowUpId { get; set; }
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
    }

    public class RecordAdministeredItemDto
    {
        public int PackageItemId { get; set; }
        public decimal ActualQuantity { get; set; }
        public string? Notes { get; set; }
    }

    public class RecordLabResultDto
    {
        public int AnalysisTypeId { get; set; }
        public string? ResultValue { get; set; }
        public string? ReferenceRange { get; set; }
        public bool? IsNormal { get; set; }
        public string? Notes { get; set; }
    }
}
