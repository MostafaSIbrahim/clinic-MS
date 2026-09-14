using Microsoft.EntityFrameworkCore;
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

    public async Task<ServiceResult<IEnumerable<ClinicDto>>> GetAllAsync(
     bool includeInactive = true)
    {
        var query = _uow.Clinics.Query();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        var dtos = await query
            .OrderBy(c => c.Name)
            .Select(c => new ClinicDto
            {
                Id = c.Id,
                Name = c.Name,
                Address = c.Address,
                Phone = c.Phone,
                IsActive = c.IsActive,

                Agreements = c.SourceAgreements
                    .Select(a => new ClinicSourceAgreementDto
                    {
                        Id = a.Id,
                        ClinicId = a.ClinicId,
                        ClinicName = c.Name,
                        PatientSourceId = a.PatientSourceId,
                        PatientSourceName = a.PatientSource.Name,
                        DeductionPercentage = a.DeductionPercentage,
                        IsActive = a.IsActive,
                        Notes = a.Notes
                    })
                    .OrderBy(a => a.PatientSourceName)
                    .ToList()
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<ClinicDto>>.Success(dtos);
    }

    public async Task<ServiceResult<ClinicDto>> GetByIdAsync(int id)
    {
        var clinic = await BuildClinicDtoAsync(id);

        if (clinic is null)
            return ServiceResult<ClinicDto>.Failure("Clinic not found.");

        return ServiceResult<ClinicDto>.Success(clinic);
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
        var createdClinic = await BuildClinicDtoAsync(clinic.Id);

        return ServiceResult<ClinicDto>.Success(
            createdClinic!,
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

        var hasReservations = await _uow.Reservations
            .Query()
            .Where(r => r.ClinicId == id)
            .AnyAsync();
        var hasPayments = await _uow.Payments
            .Query()
            .Where(p => p.ClinicId == id)
            .AnyAsync();

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

    private async Task<ClinicDto?> BuildClinicDtoAsync(int id)
    {
        return await _uow.Clinics.Query()
            .Where(c => c.Id == id)
            .Select(c => new ClinicDto
            {
                Id = c.Id,
                Name = c.Name,
                Address = c.Address,
                Phone = c.Phone,
                IsActive = c.IsActive,

                Agreements = c.SourceAgreements
                    .Select(a => new ClinicSourceAgreementDto
                    {
                        Id = a.Id,
                        ClinicId = a.ClinicId,
                        ClinicName = c.Name,
                        PatientSourceId = a.PatientSourceId,
                        PatientSourceName = a.PatientSource.Name,
                        DeductionPercentage = a.DeductionPercentage,
                        IsActive = a.IsActive,
                        Notes = a.Notes
                    })
                    .OrderBy(a => a.PatientSourceName)
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }
}
