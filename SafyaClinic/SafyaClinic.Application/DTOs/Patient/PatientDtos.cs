namespace SafyaClinic.Application.DTOs.Patient;

// ── Response ─────────────────────────────────────────────────

public class PatientDto
{
    public int Id { get; init; }
    public int? PatientSourceId { get; init; }
    public string? PatientSourceName { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateTime? DateOfBirth { get; init; }
    public int? Age => DateOfBirth.HasValue
        ? (int)((DateTime.Today - DateOfBirth.Value).TotalDays / 365.25)
        : null;
    public string? Gender { get; init; }
    public string? BloodType { get; init; }
    public string? NationalId { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? Weight { get; init; }
    public string? Allergies { get; init; }
    public string? ChronicDiseases { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }

    public IEnumerable<PatientPhoneDto> Phones { get; init; } = Enumerable.Empty<PatientPhoneDto>();
    public IEnumerable<PatientAddressDto> Addresses { get; init; } = Enumerable.Empty<PatientAddressDto>();
}

public class PatientPhoneDto
{
    public int Id { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string PhoneType { get; init; } = "Mobile";
    public bool IsPrimary { get; init; }
}

public class PatientAddressDto
{
    public int Id { get; init; }
    public string? Street { get; init; }
    public string City { get; init; } = string.Empty;
    public string? Governorate { get; init; }
    public string? PostalCode { get; init; }
    public bool IsPrimary { get; init; }
}

// ── Summary (used in lists/search) ────────────────────────────

public class PatientSummaryDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? PatientSourceName { get; init; }
    public string? NationalId { get; init; }
    public string? PrimaryPhone { get; init; }
    public int? Age { get; init; }
    public string? Gender { get; init; }
    public DateTime CreatedAt { get; init; }
}

// ── Create ────────────────────────────────────────────────────

public class CreatePatientRequest
{
    public int? PatientSourceId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateTime? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? BloodType { get; init; }
    public string? NationalId { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? Weight { get; init; }
    public string? Allergies { get; init; }
    public string? ChronicDiseases { get; init; }
    public string? Notes { get; init; }

    public List<CreatePatientPhoneRequest> Phones { get; init; } = new();
    public List<CreatePatientAddressRequest> Addresses { get; init; } = new();
}

public class CreatePatientPhoneRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string PhoneType { get; init; } = "Mobile";
    public bool IsPrimary { get; init; }
}

public class CreatePatientAddressRequest
{
    public string? Street { get; init; }
    public string City { get; init; } = string.Empty;
    public string? Governorate { get; init; }
    public string? PostalCode { get; init; }
    public bool IsPrimary { get; init; } = true;
}

// ── Update (reception can only edit basic info) ───────────────

public class UpdatePatientBasicRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? NationalId { get; init; }
    public int? PatientSourceId { get; init; }
}

public class UpdatePatientMedicalRequest
{
    public DateTime? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? BloodType { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? Weight { get; init; }
    public string? Allergies { get; init; }
    public string? ChronicDiseases { get; init; }
    public string? Notes { get; init; }
}

// ── User management ───────────────────────────────────────────

public class CreateUserRequest
{
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Specialization { get; init; }
    public string? LicenseNumber { get; init; }
    public List<int> RoleIds { get; init; } = new();
}

public class UserDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Specialization { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public IEnumerable<string> Roles { get; init; } = Enumerable.Empty<string>();
}