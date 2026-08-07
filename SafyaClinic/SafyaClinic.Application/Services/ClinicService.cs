using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Settings;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class ClinicService : IClinicService
{
    private readonly IUnitOfWork _uow;

    public ClinicService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<IEnumerable<ClinicDto>>> GetAllAsync(bool includeInactive = true)
    {
        var clinics = await _uow.Clinics.GetAllAsync();
        if (!includeInactive) clinics = clinics.Where(c => c.IsActive);

        var dtos = new List<ClinicDto>();
        foreach (var c in clinics.OrderBy(c => c.Name))
            dtos.Add(await BuildClinicDtoAsync(c.Id, c.Name, c.Address, c.Phone, c.IsActive));

        return ServiceResult<IEnumerable<ClinicDto>>.Success(dtos);
    }

    public async Task<ServiceResult<ClinicDto>> GetByIdAsync(int id)
    {
        var clinic = await _uow.Clinics.GetByIdAsync(id);
        if (clinic is null) return ServiceResult<ClinicDto>.Failure("Clinic not found.");
        return ServiceResult<ClinicDto>.Success(
            await BuildClinicDtoAsync(clinic.Id, clinic.Name, clinic.Address, clinic.Phone, clinic.IsActive));
    }

    public async Task<ServiceResult<ClinicDto>> CreateAsync(CreateClinicRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ClinicDto>.Failure("Name is required.");

        var duplicate = await _uow.Clinics.FirstOrDefaultAsync(
            c => c.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicate is not null)
            return ServiceResult<ClinicDto>.Failure("A clinic with this name already exists.");

        var clinic = new Clinic
        {
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Phone = request.Phone?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Clinics.AddAsync(clinic);
        await _uow.SaveChangesAsync();
        return ServiceResult<ClinicDto>.Success(
            await BuildClinicDtoAsync(clinic.Id, clinic.Name, clinic.Address, clinic.Phone, clinic.IsActive),
            "Clinic created.");
    }

    public async Task<ServiceResult> UpdateAsync(int id, UpdateClinicRequest request)
    {
        var clinic = await _uow.Clinics.GetByIdAsync(id);
        if (clinic is null) return ServiceResult.Failure("Clinic not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult.Failure("Name is required.");

        var duplicate = await _uow.Clinics.FirstOrDefaultAsync(
            c => c.Id != id && c.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicate is not null)
            return ServiceResult.Failure("A clinic with this name already exists.");

        clinic.Name = request.Name.Trim();
        clinic.Address = request.Address?.Trim();
        clinic.Phone = request.Phone?.Trim();
        clinic.IsActive = request.IsActive;
        clinic.UpdatedAt = DateTime.UtcNow;

        _uow.Clinics.Update(clinic);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Clinic updated.");
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var clinic = await _uow.Clinics.GetByIdAsync(id);
        if (clinic is null) return ServiceResult.Failure("Clinic not found.");

        var hasReservations = (await _uow.Reservations.FindAsync(r => r.ClinicId == id)).Any();
        var hasPayments = (await _uow.Payments.FindAsync(p => p.ClinicId == id)).Any();

        if (hasReservations || hasPayments)
        {
            clinic.IsActive = false;
            clinic.UpdatedAt = DateTime.UtcNow;
            _uow.Clinics.Update(clinic);
            await _uow.SaveChangesAsync();
            return ServiceResult.Success("Clinic has reservation/payment history, so it was deactivated instead of deleted.");
        }

        _uow.Clinics.Delete(clinic);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Clinic deleted.");
    }

    // ── Clinic ⇄ Source agreements ───────────────────────────────

    public async Task<ServiceResult<ClinicSourceAgreementDto>> UpsertAgreementAsync(UpsertClinicSourceAgreementRequest request)
    {
        if (!await _uow.Clinics.ExistsAsync(request.ClinicId))
            return ServiceResult<ClinicSourceAgreementDto>.Failure("Clinic not found.");
        if (!await _uow.PatientSources.ExistsAsync(request.PatientSourceId))
            return ServiceResult<ClinicSourceAgreementDto>.Failure("Patient source not found.");
        if (request.DeductionPercentage < 0 || request.DeductionPercentage > 100)
            return ServiceResult<ClinicSourceAgreementDto>.Failure("Deduction percentage must be between 0 and 100.");

        var existing = (await _uow.ClinicSourceAgreements.FindAsync(a =>
                a.ClinicId == request.ClinicId && a.PatientSourceId == request.PatientSourceId))
            .FirstOrDefault();

        if (existing is not null)
        {
            existing.DeductionPercentage = request.DeductionPercentage;
            existing.Notes = request.Notes?.Trim();
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            _uow.ClinicSourceAgreements.Update(existing);
        }
        else
        {
            existing = new ClinicSourceAgreement
            {
                ClinicId = request.ClinicId,
                PatientSourceId = request.PatientSourceId,
                DeductionPercentage = request.DeductionPercentage,
                Notes = request.Notes?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _uow.ClinicSourceAgreements.AddAsync(existing);
        }

        await _uow.SaveChangesAsync();

        var clinic = await _uow.Clinics.GetByIdAsync(request.ClinicId);
        var source = await _uow.PatientSources.GetByIdAsync(request.PatientSourceId);

        return ServiceResult<ClinicSourceAgreementDto>.Success(new ClinicSourceAgreementDto
        {
            Id = existing.Id,
            ClinicId = existing.ClinicId,
            ClinicName = clinic?.Name ?? "",
            PatientSourceId = existing.PatientSourceId,
            PatientSourceName = source?.Name ?? "",
            DeductionPercentage = existing.DeductionPercentage,
            IsActive = existing.IsActive,
            Notes = existing.Notes
        }, "Agreement saved.");
    }

    public async Task<ServiceResult> RemoveAgreementAsync(int agreementId)
    {
        var agreement = await _uow.ClinicSourceAgreements.GetByIdAsync(agreementId);
        if (agreement is null) return ServiceResult.Failure("Agreement not found.");

        _uow.ClinicSourceAgreements.Delete(agreement);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Agreement removed.");
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<ClinicDto> BuildClinicDtoAsync(int id, string name, string? address, string? phone, bool isActive)
    {
        var agreements = await _uow.ClinicSourceAgreements.FindAsync(a => a.ClinicId == id);
        var agreementDtos = new List<ClinicSourceAgreementDto>();
        foreach (var a in agreements)
        {
            var source = await _uow.PatientSources.GetByIdAsync(a.PatientSourceId);
            agreementDtos.Add(new ClinicSourceAgreementDto
            {
                Id = a.Id,
                ClinicId = a.ClinicId,
                ClinicName = name,
                PatientSourceId = a.PatientSourceId,
                PatientSourceName = source?.Name ?? "",
                DeductionPercentage = a.DeductionPercentage,
                IsActive = a.IsActive,
                Notes = a.Notes
            });
        }

        return new ClinicDto
        {
            Id = id,
            Name = name,
            Address = address,
            Phone = phone,
            IsActive = isActive,
            Agreements = agreementDtos.OrderBy(a => a.PatientSourceName)
        };
    }
}
