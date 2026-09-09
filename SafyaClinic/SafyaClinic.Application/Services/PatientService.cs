using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;


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
        var createdPatient = await GetPatientDtoByIdAsync(patient.Id);
        return ServiceResult<PatientDto>.Success(
            createdPatient,
            "Patient created successfully.");
    }

    public async Task<ServiceResult<PatientDto>> GetPatientByIdAsync(int patientId)
    {
        var patient = await GetPatientDtoByIdAsync(patientId);
        if (patient is null)
            return ServiceResult<PatientDto>.Failure("Patient not found.");

        return ServiceResult<PatientDto>.Success(patient);
    }

    public async Task<ServiceResult<PagedResult<PatientSummaryDto>>> SearchPatientsAsync(
        PaginationRequest request)
    {
        var all = _uow.Patients.Query();
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            all = all.Where(p =>
               p.FirstName.Contains(search) ||
               p.LastName.Contains(search) ||
               (p.NationalId != null && p.NationalId.Contains(search)) ||
               p.Phones.Any(ph => ph.PhoneNumber.Contains(search)));
        }
        var totalCount = await all.CountAsync();
        var patients = await all
                .OrderBy(p => p.Id) // Default ordering; can be enhanced based on request.SortBy
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new
                {
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    PatientSourceName = p.PatientSource != null ? p.PatientSource.Name : null,
                    p.NationalId,
                    PrimaryPhone = p.Phones
                        .Where(ph => ph.IsPrimary)
                        .OrderBy(ph => ph.Id) // Ensure consistent ordering
                        .Select(ph => ph.PhoneNumber)
                        .FirstOrDefault(),
                    p.DateOfBirth,
                    p.Gender,
                    p.CreatedAt,
                })
                .ToListAsync();
        var summaries = patients.Select(p => new PatientSummaryDto
        {
            Id = p.Id,
            FullName = $"{p.FirstName} {p.LastName}",
            PatientSourceName = p.PatientSourceName,
            NationalId = p.NationalId,
            PrimaryPhone = p.PrimaryPhone,
            Age = p.DateOfBirth.HasValue
                    ? (int)((DateTime.Today - p.DateOfBirth.Value).TotalDays / 365.25)
                    : null,
            Gender = p.Gender != null ? p.Gender.ToString() : null,
            CreatedAt = p.CreatedAt
        }).ToList();
        if (!patients.Any())
            return ServiceResult<PagedResult<PatientSummaryDto>>.Success(new PagedResult<PatientSummaryDto>
            {
                Items = new List<PatientSummaryDto>(),
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            });


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
    private async Task<PatientDto> GetPatientDtoByIdAsync(int patientId)
    {
        var patient = await _uow.Patients.Query()
             .Where(p => p.Id == patientId)
             .Select(p => new PatientDto
             {
                 Id = p.Id,
                 PatientSourceId = p.PatientSourceId,
                 PatientSourceName = p.PatientSource != null ? p.PatientSource.Name : null,
                 FirstName = p.FirstName,
                 LastName = p.LastName,
                 DateOfBirth = p.DateOfBirth,
                 Gender = p.Gender != null ? p.Gender.ToString() : null,
                 BloodType = p.BloodType != null ? p.BloodType.ToString() : null,
                 NationalId = p.NationalId,
                 HeightCm = p.HeightCm,
                 Weight = p.Weight,
                 Allergies = p.Allergies,
                 ChronicDiseases = p.ChronicDiseases,
                 Notes = p.Notes,
                 CreatedAt = p.CreatedAt,
                 Phones = p.Phones.Select(ph => new PatientPhoneDto
                 {
                     Id = ph.Id,
                     PhoneNumber = ph.PhoneNumber,
                     PhoneType = ph.PhoneType ?? "Mobile",
                     IsPrimary = ph.IsPrimary
                 }),
                 Addresses = p.Addresses.Select(a => new PatientAddressDto
                 {
                     Id = a.Id,
                     Street = a.Street,
                     City = a.City,
                     Governorate = a.Governorate,
                     PostalCode = a.PostalCode,
                     IsPrimary = a.IsPrimary
                 })
             }).FirstOrDefaultAsync();

        return patient ?? throw new Exception("Patient not found.");
    }
}