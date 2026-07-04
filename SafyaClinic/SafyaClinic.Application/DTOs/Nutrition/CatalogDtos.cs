namespace SafyaClinic.Application.DTOs.Nutrition
{
    // ── Injection Types ──────────────────────────────────────────

    public class InjectionTypeDto
    {
        public int Id { get; set; }
        public string InjectionName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultDosage { get; set; }
        public bool IsActive { get; set; }
        public bool IsInUse { get; set; }
    }

    public class CreateInjectionTypeDto
    {
        public string InjectionName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultDosage { get; set; }
    }

    public class UpdateInjectionTypeDto
    {
        public string InjectionName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultDosage { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ── Vitamin Types ────────────────────────────────────────────

    public class VitaminTypeDto
    {
        public int Id { get; set; }
        public string VitaminName { get; set; } = string.Empty;
        public string? Formulation { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsInUse { get; set; }
    }

    public class CreateVitaminTypeDto
    {
        public string VitaminName { get; set; } = string.Empty;
        public string? Formulation { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateVitaminTypeDto
    {
        public string VitaminName { get; set; } = string.Empty;
        public string? Formulation { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
