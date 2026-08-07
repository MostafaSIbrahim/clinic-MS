namespace SafyaClinic.Application.DTOs.Settings;

// ── Patient Source ───────────────────────────────────────────

public class PatientSourceDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal DefaultDeductionPercentage { get; init; }
    public bool IsActive { get; init; }
    public int PatientCount { get; init; }
}

public class CreatePatientSourceRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal DefaultDeductionPercentage { get; init; }
}

public class UpdatePatientSourceRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal DefaultDeductionPercentage { get; init; }
    public bool IsActive { get; init; } = true;
}

// ── Clinic ────────────────────────────────────────────────────

public class ClinicDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
    public IEnumerable<ClinicSourceAgreementDto> Agreements { get; init; } = Enumerable.Empty<ClinicSourceAgreementDto>();
}

public class CreateClinicRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
}

public class UpdateClinicRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; } = true;
}

// ── Clinic ⇄ Source Agreement ────────────────────────────────

public class ClinicSourceAgreementDto
{
    public int Id { get; init; }
    public int ClinicId { get; init; }
    public string ClinicName { get; init; } = string.Empty;
    public int PatientSourceId { get; init; }
    public string PatientSourceName { get; init; } = string.Empty;
    public decimal DeductionPercentage { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
}

public class UpsertClinicSourceAgreementRequest
{
    public int ClinicId { get; init; }
    public int PatientSourceId { get; init; }
    public decimal DeductionPercentage { get; init; }
    public string? Notes { get; init; }
}
