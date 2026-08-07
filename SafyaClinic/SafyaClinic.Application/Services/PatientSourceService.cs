using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Settings;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class PatientSourceService : IPatientSourceService
{
    private readonly IUnitOfWork _uow;

    public PatientSourceService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<IEnumerable<PatientSourceDto>>> GetAllAsync(bool includeInactive = true)
    {
        var sources = await _uow.PatientSources.GetAllAsync();
        if (!includeInactive) sources = sources.Where(s => s.IsActive);

        var dtos = new List<PatientSourceDto>();
        foreach (var s in sources.OrderBy(s => s.Name))
        {
            var count = await _uow.Patients.CountAsync(p => p.PatientSourceId == s.Id);
            dtos.Add(ToDto(s, count));
        }

        return ServiceResult<IEnumerable<PatientSourceDto>>.Success(dtos);
    }

    public async Task<ServiceResult<PatientSourceDto>> GetByIdAsync(int id)
    {
        var source = await _uow.PatientSources.GetByIdAsync(id);
        if (source is null) return ServiceResult<PatientSourceDto>.Failure("Patient source not found.");
        var count = await _uow.Patients.CountAsync(p => p.PatientSourceId == id);
        return ServiceResult<PatientSourceDto>.Success(ToDto(source, count));
    }

    public async Task<ServiceResult<PatientSourceDto>> CreateAsync(CreatePatientSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<PatientSourceDto>.Failure("Name is required.");
        if (request.DefaultDeductionPercentage < 0 || request.DefaultDeductionPercentage > 100)
            return ServiceResult<PatientSourceDto>.Failure("Deduction percentage must be between 0 and 100.");

        var duplicate = await _uow.PatientSources.FirstOrDefaultAsync(
            s => s.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicate is not null)
            return ServiceResult<PatientSourceDto>.Failure("A source with this name already exists.");

        var source = new PatientSource
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DefaultDeductionPercentage = request.DefaultDeductionPercentage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.PatientSources.AddAsync(source);
        await _uow.SaveChangesAsync();
        return ServiceResult<PatientSourceDto>.Success(ToDto(source, 0), "Patient source created.");
    }

    public async Task<ServiceResult> UpdateAsync(int id, UpdatePatientSourceRequest request)
    {
        var source = await _uow.PatientSources.GetByIdAsync(id);
        if (source is null) return ServiceResult.Failure("Patient source not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult.Failure("Name is required.");
        if (request.DefaultDeductionPercentage < 0 || request.DefaultDeductionPercentage > 100)
            return ServiceResult.Failure("Deduction percentage must be between 0 and 100.");

        var duplicate = await _uow.PatientSources.FirstOrDefaultAsync(
            s => s.Id != id && s.Name.ToLower() == request.Name.Trim().ToLower());
        if (duplicate is not null)
            return ServiceResult.Failure("A source with this name already exists.");

        source.Name = request.Name.Trim();
        source.Description = request.Description?.Trim();
        source.DefaultDeductionPercentage = request.DefaultDeductionPercentage;
        source.IsActive = request.IsActive;
        source.UpdatedAt = DateTime.UtcNow;

        _uow.PatientSources.Update(source);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Patient source updated.");
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var source = await _uow.PatientSources.GetByIdAsync(id);
        if (source is null) return ServiceResult.Failure("Patient source not found.");

        // If the source is already referenced by payment history, deactivate instead of a hard delete
        // to keep historical financial records intact.
        var hasPaymentHistory = (await _uow.Payments.FindAsync(p => p.PatientSourceId == id)).Any();
        if (hasPaymentHistory)
        {
            source.IsActive = false;
            source.UpdatedAt = DateTime.UtcNow;
            _uow.PatientSources.Update(source);
            await _uow.SaveChangesAsync();
            return ServiceResult.Success("Source has payment history, so it was deactivated instead of deleted.");
        }

        _uow.PatientSources.Delete(source);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Patient source deleted.");
    }

    private static PatientSourceDto ToDto(PatientSource s, int patientCount) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        DefaultDeductionPercentage = s.DefaultDeductionPercentage,
        IsActive = s.IsActive,
        PatientCount = patientCount
    };
}
