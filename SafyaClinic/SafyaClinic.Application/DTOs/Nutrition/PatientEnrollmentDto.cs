using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Application.DTOs.Nutrition
{
    public class PatientEnrollmentDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal BasePrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalPaid { get; set; }
        public decimal RemainingAmount => FinalPrice - TotalPaid;
        public List<WeeklyFollowUpDto> WeeklyFollowUps { get; set; } = new List<WeeklyFollowUpDto>();
    }
    public class CreateEnrollmentDto
    {
        public int PatientId { get; set; }
        public int PackageId { get; set; }
        public int DoctorId { get; set; }
        public DateTime StartDate { get; set; }
        public decimal DiscountPercent { get; set; }
        public string? Notes { get; set; }
    }
}
