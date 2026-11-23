using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Settings;

/// <summary>The ClinicInfo server setting. Since M5d it only gives the starting values until the clinic profile is saved.</summary>
public sealed class ClinicInfoOptions
{
    public const string SectionName = "ClinicInfo";
    public string? Name { get; set; }
    public string? Tagline { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

/// <summary>M5d: the clinic's details for printouts. <see cref="IsSaved"/> is false while they still come from ClinicInfo.</summary>
public sealed class ClinicProfileDto
{
    public string NameEn { get; init; } = string.Empty;
    public string? NameAr { get; init; }
    public string? TaglineEn { get; init; }
    public string? TaglineAr { get; init; }
    public string? AddressEn { get; init; }
    public string? AddressAr { get; init; }
    public string? Phone { get; init; }
    public string? SecondPhone { get; init; }
    public string? Email { get; init; }
    public string? LicenceLine { get; init; }
    public bool IsSaved { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>The phone numbers that are filled in, for one line on a printout.</summary>
    public IEnumerable<string> Phones => new[] { Phone, SecondPhone }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!);
}

/// <summary>The clinic profile form.</summary>
public sealed class ClinicProfileForm
{
    public string? NameEn { get; set; }
    public string? NameAr { get; set; }
    public string? TaglineEn { get; set; }
    public string? TaglineAr { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressAr { get; set; }
    public string? Phone { get; set; }
    public string? SecondPhone { get; set; }
    public string? Email { get; set; }
    public string? LicenceLine { get; set; }
}

public interface IClinicProfileService
{
    /// <summary>The saved profile, or the ClinicInfo values if it hasn't been saved yet.</summary>
    Task<ClinicProfileDto> GetAsync();
    Task<ServiceResult> SaveAsync(ClinicProfileForm form, int userId);
}

public sealed class ClinicProfileService : IClinicProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail _audit;
    private readonly ClinicInfoOptions _defaults;

    public ClinicProfileService(IUnitOfWork uow, IAuditTrail audit, IOptions<ClinicInfoOptions> defaults)
    {
        _uow = uow;
        _audit = audit;
        _defaults = defaults.Value;
    }

    public async Task<ClinicProfileDto> GetAsync()
    {
        var p = await _uow.ClinicProfiles.Query().OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (p is null)
            return new ClinicProfileDto
            {
                NameEn = Clean(_defaults.Name) ?? "Clinic",
                TaglineEn = Clean(_defaults.Tagline),
                AddressEn = Clean(_defaults.Address),
                Phone = Clean(_defaults.Phone),
                Email = Clean(_defaults.Email),
                IsSaved = false,
            };
        return new ClinicProfileDto
        {
            NameEn = p.NameEn, NameAr = p.NameAr, TaglineEn = p.TaglineEn, TaglineAr = p.TaglineAr,
            AddressEn = p.AddressEn, AddressAr = p.AddressAr, Phone = p.Phone, SecondPhone = p.SecondPhone,
            Email = p.Email, LicenceLine = p.LicenceLine, IsSaved = true, UpdatedAt = p.UpdatedAt ?? p.CreatedAt,
        };
    }

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9][0-9 ()\-]{4,28}$", RegexOptions.Compiled);

    /// <summary>The checks, field by field. The English name, address and one phone are required.</summary>
    public static Dictionary<string, string> Check(ClinicProfileForm f)
    {
        var problems = new Dictionary<string, string>();
        void Length(string field, string? value, int max)
        {
            if (Clean(value) is { } v && v.Length > max) problems[field] = $"This can be at most {max} characters.";
        }

        if (Clean(f.NameEn) is null) problems[nameof(f.NameEn)] = "Enter the clinic's name in English.";
        if (Clean(f.AddressEn) is null) problems[nameof(f.AddressEn)] = "Enter the address in English.";
        if (Clean(f.Phone) is null) problems[nameof(f.Phone)] = "Enter a phone number.";
        Length(nameof(f.NameEn), f.NameEn, 150);
        Length(nameof(f.NameAr), f.NameAr, 150);
        Length(nameof(f.TaglineEn), f.TaglineEn, 200);
        Length(nameof(f.TaglineAr), f.TaglineAr, 200);
        Length(nameof(f.AddressEn), f.AddressEn, 300);
        Length(nameof(f.AddressAr), f.AddressAr, 300);
        Length(nameof(f.LicenceLine), f.LicenceLine, 200);
        foreach (var (field, value) in new[] { (nameof(f.Phone), f.Phone), (nameof(f.SecondPhone), f.SecondPhone) })
            if (Clean(value) is { } phone && !PhonePattern.IsMatch(phone))
                problems[field] = "Enter a phone number using digits, spaces, dashes and an optional +.";
        if (Clean(f.Email) is { } email && (email.Length > 150 || !EmailPattern.IsMatch(email)))
            problems[nameof(f.Email)] = "Enter a valid email address, or leave it empty.";
        return problems;
    }

    public async Task<ServiceResult> SaveAsync(ClinicProfileForm f, int userId)
    {
        var problems = Check(f);
        if (problems.Count > 0) return ServiceResult.Failure(problems.Values.ToList());

        var p = await _uow.ClinicProfiles.Query().OrderBy(x => x.Id).FirstOrDefaultAsync();
        var isNew = p is null;
        p ??= new ClinicProfile { CreatedAt = DateTime.UtcNow, CreatedBy = userId };
        p.NameEn = Clean(f.NameEn)!;
        p.NameAr = Clean(f.NameAr);
        p.TaglineEn = Clean(f.TaglineEn);
        p.TaglineAr = Clean(f.TaglineAr);
        p.AddressEn = Clean(f.AddressEn);
        p.AddressAr = Clean(f.AddressAr);
        p.Phone = Clean(f.Phone);
        p.SecondPhone = Clean(f.SecondPhone);
        p.Email = Clean(f.Email);
        p.LicenceLine = Clean(f.LicenceLine);

        if (isNew) await _uow.ClinicProfiles.AddAsync(p);
        else
        {
            p.UpdatedAt = DateTime.UtcNow;
            _uow.ClinicProfiles.Update(p);
        }
        await _audit.RecordAsync(AuditActions.ClinicProfileSaved, nameof(ClinicProfile), isNew ? null : p.Id,
            new { p.NameEn, p.NameAr, p.AddressEn, p.Phone, p.Email }, userId);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Clinic profile saved. Prescriptions and lab slips now print these details.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
