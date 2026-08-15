namespace SafyaClinic.Application.DTOs.Reservation;

public class ReservationDto
{
    public int Id { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public int ClinicId { get; init; }
    public string ClinicName { get; init; } = string.Empty;
    public int TreatmentTypeId { get; init; }
    public string TreatmentTypeName { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public string StatusColor { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;   // InternalMedicine | Nutritional
    public DateTime ReservationDate { get; init; }
    public TimeSpan ReservationTime { get; init; }
    public int DurationMinutes { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public bool IsPaid { get; init; }
    public decimal? TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class ReservationSummaryDto
{
    public int Id { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string DoctorName { get; init; } = string.Empty;
    public string ClinicName { get; init; } = string.Empty;
    public string TreatmentTypeName { get; init; } = string.Empty;
    public DateTime ReservationDate { get; init; }
    public TimeSpan ReservationTime { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string StatusColor { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsPaid { get; init; }
}

public class CreateReservationRequest
{
    public int PatientId { get; init; }
    public int DoctorId { get; init; }
    public int ClinicId { get; init; }
    public int TreatmentTypeId { get; init; }
    public string Category { get; init; } = "InternalMedicine";
    public DateTime ReservationDate { get; init; }
    public TimeSpan ReservationTime { get; init; }
    public int DurationMinutes { get; init; } = 30;
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public decimal? TotalAmount { get; init; }
}

public class UpdateReservationRequest
{
    public int DoctorId { get; init; }
    public int ClinicId { get; init; }
    public int TreatmentTypeId { get; init; }
    public int StatusId { get; init; }
    public DateTime ReservationDate { get; init; }
    public TimeSpan ReservationTime { get; init; }
    public int DurationMinutes { get; init; } = 30;
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public decimal? TotalAmount { get; init; }
}

public class ReservationFilterRequest
{
    public int? DoctorId { get; init; }
    public int? PatientId { get; init; }
    public int? ClinicId { get; init; }
    public int? StatusId { get; init; }
    public string? Category { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public bool? IsPaid { get; init; }
}