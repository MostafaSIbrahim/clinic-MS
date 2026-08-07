using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class PatientService : IPatientService
{
    private readonly IUnitOfWork _uow;

    public PatientService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<PatientDto>> CreatePatientAsync(
        CreatePatientRequest request, int createdByUserId)
    {
        // Prevent duplicate national IDs
        if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            var duplicate = await _uow.Patients.FirstOrDefaultAsync(
                p => p.NationalId == request.NationalId);
            if (duplicate is not null)
                return ServiceResult<PatientDto>.Failure(
                    "A patient with this National ID already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.Gender) &&
            !Enum.TryParse<Gender>(request.Gender, out _))
            return ServiceResult<PatientDto>.Failure("Invalid gender value.");

        if (!string.IsNullOrWhiteSpace(request.BloodType) &&
            !Enum.TryParse<BloodType>(request.BloodType, out _))
            return ServiceResult<PatientDto>.Failure("Invalid blood type value.");

        var patient = new Patient
        {
            PatientSourceId = request.PatientSourceId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : Enum.Parse<Gender>(request.Gender),
            BloodType = string.IsNullOrWhiteSpace(request.BloodType) ? null : Enum.Parse<BloodType>(request.BloodType),
            NationalId = request.NationalId?.Trim(),
            HeightCm = request.HeightCm,
            Weight = request.Weight,
            Allergies = request.Allergies?.Trim(),
            ChronicDiseases = request.ChronicDiseases?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdByUserId
        };

        await _uow.Patients.AddAsync(patient);
        await _uow.SaveChangesAsync();

        // Phones
        foreach (var ph in request.Phones)
        {
            await _uow.PatientPhones.AddAsync(new PatientPhone
            {
                PatientId = patient.Id,
                PhoneNumber = ph.PhoneNumber.Trim(),
                PhoneType = ph.PhoneType,
                IsPrimary = ph.IsPrimary
            });
        }

        // Addresses
        foreach (var addr in request.Addresses)
        {
            await _uow.PatientAddresses.AddAsync(new PatientAddress
            {
                PatientId = patient.Id,
                Street = addr.Street?.Trim(),
                City = addr.City.Trim(),
                Governorate = addr.Governorate?.Trim(),
                PostalCode = addr.PostalCode?.Trim(),
                IsPrimary = addr.IsPrimary
            });
        }

        await _uow.SaveChangesAsync();

        return ServiceResult<PatientDto>.Success(
            await BuildPatientDtoAsync(patient),
            "Patient created successfully.");
    }

    public async Task<ServiceResult<PatientDto>> GetPatientByIdAsync(int patientId)
    {
        var patient = await _uow.Patients.GetByIdAsync(patientId);
        if (patient is null)
            return ServiceResult<PatientDto>.Failure("Patient not found.");

        return ServiceResult<PatientDto>.Success(await BuildPatientDtoAsync(patient));
    }

    public async Task<ServiceResult<PagedResult<PatientSummaryDto>>> SearchPatientsAsync(
        PaginationRequest request)
    {
        var all = await _uow.Patients.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.ToLower();
            // Find patients whose phone numbers match the search
            var matchingPhones = await _uow.PatientPhones.FindAsync(
                ph => ph.PhoneNumber.Contains(q));
            var patientIdsWithMatchingPhone = matchingPhones
                .Select(ph => ph.PatientId)
                .ToHashSet();
            // In-memory filter — replace with IQueryable extension for production
            all = all.Where(p =>
            p.FirstName.ToLower().Contains(q) ||
            p.LastName.ToLower().Contains(q) ||
            (p.NationalId != null && p.NationalId.Contains(q)) ||
            patientIdsWithMatchingPhone.Contains(p.Id));
        }

        var totalCount = all.Count();
        var paged = all
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var summaries = new List<PatientSummaryDto>();
        foreach (var p in paged)
        {
            var primaryPhone = (await _uow.PatientPhones.FindAsync(
                ph => ph.PatientId == p.Id && ph.IsPrimary))
                .FirstOrDefault()?.PhoneNumber;

            var source = p.PatientSourceId.HasValue
                ? await _uow.PatientSources.GetByIdAsync(p.PatientSourceId.Value)
                : null;

            summaries.Add(new PatientSummaryDto
            {
                Id = p.Id,
                FullName = $"{p.FirstName} {p.LastName}",
                PatientSourceName = source?.Name,
                NationalId = p.NationalId,
                PrimaryPhone = primaryPhone,
                Age = p.DateOfBirth.HasValue
                    ? (int)((DateTime.Today - p.DateOfBirth.Value).TotalDays / 365.25)
                    : null,
                Gender = p.Gender?.ToString(),
                CreatedAt = p.CreatedAt
            });
        }

        return ServiceResult<PagedResult<PatientSummaryDto>>.Success(new PagedResult<PatientSummaryDto>
        {
            Items = summaries,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    public async Task<ServiceResult> UpdateBasicInfoAsync(
        int patientId, UpdatePatientBasicRequest request)
    {
        var patient = await _uow.Patients.GetByIdAsync(patientId);
        if (patient is null)
            return ServiceResult.Failure("Patient not found.");

        // National ID uniqueness (if changed)
        if (!string.IsNullOrWhiteSpace(request.NationalId) &&
            request.NationalId != patient.NationalId)
        {
            var dup = await _uow.Patients.FirstOrDefaultAsync(
                p => p.NationalId == request.NationalId && p.Id != patientId);
            if (dup is not null)
                return ServiceResult.Failure("National ID already used by another patient.");
        }

        patient.FirstName = request.FirstName.Trim();
        patient.LastName = request.LastName.Trim();
        patient.NationalId = request.NationalId?.Trim();
        patient.PatientSourceId = request.PatientSourceId;
        patient.UpdatedAt = DateTime.UtcNow;

        _uow.Patients.Update(patient);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Patient updated.");
    }

    public async Task<ServiceResult> UpdateMedicalInfoAsync(
        int patientId, UpdatePatientMedicalRequest request)
    {
        var patient = await _uow.Patients.GetByIdAsync(patientId);
        if (patient is null)
            return ServiceResult.Failure("Patient not found.");

        patient.DateOfBirth = request.DateOfBirth;
        patient.HeightCm = request.HeightCm;
        patient.Weight = request.Weight;
        patient.Allergies = request.Allergies?.Trim();
        patient.ChronicDiseases = request.ChronicDiseases?.Trim();
        patient.Notes = request.Notes?.Trim();
        patient.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Gender))
            patient.Gender = Enum.Parse<Gender>(request.Gender);
        if (!string.IsNullOrWhiteSpace(request.BloodType))
            patient.BloodType = Enum.Parse<BloodType>(request.BloodType);

        _uow.Patients.Update(patient);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Medical info updated.");
    }

    public async Task<ServiceResult> AddPhoneAsync(
        int patientId, CreatePatientPhoneRequest request)
    {
        if (!await _uow.Patients.ExistsAsync(patientId))
            return ServiceResult.Failure("Patient not found.");

        await _uow.PatientPhones.AddAsync(new PatientPhone
        {
            PatientId = patientId,
            PhoneNumber = request.PhoneNumber.Trim(),
            PhoneType = request.PhoneType,
            IsPrimary = request.IsPrimary
        });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Phone added.");
    }

    public async Task<ServiceResult> RemovePhoneAsync(int patientId, int phoneId)
    {
        var phone = await _uow.PatientPhones.FirstOrDefaultAsync(
            ph => ph.Id == phoneId && ph.PatientId == patientId);
        if (phone is null)
            return ServiceResult.Failure("Phone not found for this patient.");

        _uow.PatientPhones.Delete(phone);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Phone removed.");
    }

    public async Task<ServiceResult> AddAddressAsync(
        int patientId, CreatePatientAddressRequest request)
    {
        if (!await _uow.Patients.ExistsAsync(patientId))
            return ServiceResult.Failure("Patient not found.");

        await _uow.PatientAddresses.AddAsync(new PatientAddress
        {
            PatientId = patientId,
            Street = request.Street?.Trim(),
            City = request.City.Trim(),
            Governorate = request.Governorate?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            IsPrimary = request.IsPrimary
        });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Address added.");
    }

    public async Task<ServiceResult> RemoveAddressAsync(int patientId, int addressId)
    {
        var address = await _uow.PatientAddresses.FirstOrDefaultAsync(
            a => a.Id == addressId && a.PatientId == patientId);
        if (address is null)
            return ServiceResult.Failure("Address not found for this patient.");

        _uow.PatientAddresses.Delete(address);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Address removed.");
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<PatientDto> BuildPatientDtoAsync(Patient patient)
    {
        
        var phones = await _uow.PatientPhones.FindAsync(ph => ph.PatientId == patient.Id);
        var addresses = await _uow.PatientAddresses.FindAsync(a => a.PatientId == patient.Id);
        var source = patient.PatientSourceId.HasValue
            ? await _uow.PatientSources.GetByIdAsync(patient.PatientSourceId.Value)
            : null;

       
        return new PatientDto
        {
            Id = patient.Id,
            PatientSourceId = patient.PatientSourceId,
            PatientSourceName = source?.Name,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender?.ToString(),
            BloodType = patient.BloodType?.ToString(),
            NationalId = patient.NationalId,
            HeightCm = patient.HeightCm,
            Weight = patient.Weight,
            Allergies = patient.Allergies,
            ChronicDiseases = patient.ChronicDiseases,
            Notes = patient.Notes,
            CreatedAt = patient.CreatedAt,
            Phones = phones.Select(ph => new PatientPhoneDto
            {
                Id = ph.Id,
                PhoneNumber = ph.PhoneNumber,
                PhoneType = ph.PhoneType ?? "Mobile",
                IsPrimary = ph.IsPrimary
            }),
            Addresses = addresses.Select(a => new PatientAddressDto
            {
                Id = a.Id,
                Street = a.Street,
                City = a.City,
                Governorate = a.Governorate,
                PostalCode = a.PostalCode,
                IsPrimary = a.IsPrimary
            })
        };
    }
}