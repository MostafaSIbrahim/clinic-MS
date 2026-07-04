using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class NutritionService : INutritionService
{
    private readonly IUnitOfWork _uow;

    public NutritionService(IUnitOfWork uow) => _uow = uow;

    // ── Packages ─────────────────────────────────────────────

    public async Task<ServiceResult<NutritionPackageDto>> CreatePackageAsync(
        CreateNutritionPackageDto request, int createdBy)
    {
        if (request.BasePrice <= 0)
            return ServiceResult<NutritionPackageDto>.Failure("Base price must be greater than zero.");
        if (request.MaxDiscountPercent < 0 || request.MaxDiscountPercent > 100)
            return ServiceResult<NutritionPackageDto>.Failure("Max discount must be between 0 and 100.");

        // Validate items reference valid injection/vitamin IDs
        foreach (var item in request.Items)
        {
            if (item.InjectionId is null && item.VitaminId is null)
                return ServiceResult<NutritionPackageDto>.Failure(
                    "Each package item must have either an injection or a vitamin.");
            if (item.InjectionId.HasValue && !await _uow.InjectionTypes.ExistsAsync(item.InjectionId.Value))
                return ServiceResult<NutritionPackageDto>.Failure(
                    $"Injection type ID {item.InjectionId} not found.");
            if (item.VitaminId.HasValue && !await _uow.VitaminTypes.ExistsAsync(item.VitaminId.Value))
                return ServiceResult<NutritionPackageDto>.Failure(
                    $"Vitamin type ID {item.VitaminId} not found.");
            if (item.WeekNumber < 1 || item.WeekNumber > 4)
                return ServiceResult<NutritionPackageDto>.Failure(
                    "Week number must be between 1 and 4.");
        }

        var package = new NutritionPackage
        {
            PackageName = request.PackageName.Trim(),
            Description = request.Description?.Trim(),
            DurationWeeks = 4,          // Fixed per business rule
            SessionsPerWeek = 1,          // Fixed per business rule
            BasePrice = request.BasePrice,
            MaxDiscountPercent = request.MaxDiscountPercent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.NutritionPackages.AddAsync(package);
        await _uow.SaveChangesAsync();

        foreach (var item in request.Items)
        {
            await _uow.PackageItems.AddAsync(new PackageItem
            {
                PackageId = package.Id,
                InjectionId = item.InjectionId,
                VitaminId = item.VitaminId,
                Quantity = item.Quantity,
                Unit = item.Unit?.Trim(),
                WeekNumber = item.WeekNumber,
                Notes = item.Notes?.Trim()
            });
        }

        await _uow.SaveChangesAsync();
        return ServiceResult<NutritionPackageDto>.Success(
            await BuildPackageDtoAsync(package.Id));
    }

    public async Task<ServiceResult<NutritionPackageDto>> GetPackageByIdAsync(int packageId)
    {
        var package = await _uow.NutritionPackages.GetPackageWithItemsAsync(packageId);
        if (package is null)
            return ServiceResult<NutritionPackageDto>.Failure("Package not found.");
        return ServiceResult<NutritionPackageDto>.Success(MapPackageDto(package));
    }

    public async Task<ServiceResult<IEnumerable<NutritionPackageDto>>> GetActivePackagesAsync()
    {
        var packages = await _uow.NutritionPackages.GetActivePackagesAsync();
        return ServiceResult<IEnumerable<NutritionPackageDto>>.Success(
            packages.Select(MapPackageDto));
    }

    public async Task<ServiceResult> DeactivatePackageAsync(int packageId)
    {
        var package = await _uow.NutritionPackages.GetByIdAsync(packageId);
        if (package is null) return ServiceResult.Failure("Package not found.");

        package.IsActive = false;
        _uow.NutritionPackages.Update(package);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Package deactivated.");
    }

    // ── Enrollments ──────────────────────────────────────────

    public async Task<ServiceResult<PatientEnrollmentDto>> EnrollPatientAsync(
        CreateEnrollmentDto request, int enrolledBy)
    {
        if (!await _uow.Patients.ExistsAsync(request.PatientId))
            return ServiceResult<PatientEnrollmentDto>.Failure("Patient not found.");
        if (!await _uow.Users.ExistsAsync(request.DoctorId))
            return ServiceResult<PatientEnrollmentDto>.Failure("Doctor not found.");

        var package = await _uow.NutritionPackages.GetByIdAsync(request.PackageId);
        if (package is null || !package.IsActive)
            return ServiceResult<PatientEnrollmentDto>.Failure("Package not found or inactive.");

        // Business rule: discount cannot exceed package maximum
        if (request.DiscountPercent < 0 || request.DiscountPercent > package.MaxDiscountPercent)
            return ServiceResult<PatientEnrollmentDto>.Failure(
                $"Discount cannot exceed {package.MaxDiscountPercent}% for this package.");

        // Business rule: no active enrollment for same patient in overlapping dates
        var existing = await _uow.NutritionEnrollments.FindAsync(
            e => e.PatientId == request.PatientId && e.Status == EnrollmentStatus.Active);
        if (existing.Any())
            return ServiceResult<PatientEnrollmentDto>.Failure(
                "Patient already has an active nutrition enrollment.");

        var startDate = request.StartDate.Date;
        var endDate = startDate.AddDays(package.DurationWeeks * 7);

        var enrollment = new PatientNutritionEnrollment
        {
            PatientId = request.PatientId,
            PackageId = request.PackageId,
            DoctorId = request.DoctorId,
            StartDate = startDate,
            EndDate = endDate,
            BasePrice = package.BasePrice,
            DiscountPercent = request.DiscountPercent,
            Status = EnrollmentStatus.Active,
            TotalPaid = 0,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = enrolledBy
        };

        await _uow.NutritionEnrollments.AddAsync(enrollment);
        await _uow.SaveChangesAsync();
        return ServiceResult<PatientEnrollmentDto>.Success(
            await BuildEnrollmentDtoAsync(enrollment));
    }

    public async Task<ServiceResult<PatientEnrollmentDto>> GetEnrollmentByIdAsync(int enrollmentId)
    {
        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null)
            return ServiceResult<PatientEnrollmentDto>.Failure("Enrollment not found.");
        return ServiceResult<PatientEnrollmentDto>.Success(
            await BuildEnrollmentDtoAsync(enrollment));
    }

    public async Task<ServiceResult<IEnumerable<PatientEnrollmentDto>>> GetPatientEnrollmentsAsync(
        int patientId)
    {
        var enrollments = await _uow.NutritionEnrollments.FindAsync(
            e => e.PatientId == patientId);
        var dtos = new List<PatientEnrollmentDto>();
        foreach (var e in enrollments.OrderByDescending(e => e.StartDate))
            dtos.Add(await BuildEnrollmentDtoAsync(e));
        return ServiceResult<IEnumerable<PatientEnrollmentDto>>.Success(dtos);
    }

    public async Task<ServiceResult> UpdateEnrollmentStatusAsync(int enrollmentId, string status)
    {
        if (!Enum.TryParse<EnrollmentStatus>(status, out var enrollmentStatus))
            return ServiceResult.Failure(
                "Invalid status. Valid: Active, Completed, Cancelled, OnHold.");

        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null) return ServiceResult.Failure("Enrollment not found.");

        enrollment.Status = enrollmentStatus;
        _uow.NutritionEnrollments.Update(enrollment);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Enrollment status updated.");
    }

    // ── Weekly Follow-Ups ────────────────────────────────────

    public async Task<ServiceResult<WeeklyFollowUpDto>> RecordFollowUpAsync(
        int enrollmentId, RecordFollowUpDto request, int recordedBy)
    {
        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Enrollment not found.");
        if (enrollment.Status != EnrollmentStatus.Active)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Enrollment is not active.");

        // Determine next week number
        var existingFollowUps = await _uow.WeeklyFollowUps.GetByEnrollmentAsync(enrollmentId);
        var nextWeek = existingFollowUps.Any()
            ? existingFollowUps.Max(f => f.WeekNumber) + 1
            : 1;

        if (nextWeek > 4)
            return ServiceResult<WeeklyFollowUpDto>.Failure(
                "All 4 weekly follow-ups are already recorded for this enrollment.");

        var followUp = new WeeklyFollowUp
        {
            EnrollmentId = enrollmentId,
            WeekNumber = nextWeek,
            FollowUpDate = request.FollowUpDate,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
            BodyFatPercent = request.BodyFatPercent,
            MuscleMassKg = request.MuscleMassKg,
            WaistCircumferenceCm = request.WaistCircumferenceCm,
            BloodPressureSys = request.BloodPressureSys,
            BloodPressureDia = request.BloodPressureDia,
            LabResultsSummary = request.LabResultsSummary?.Trim(),
            DoctorNotes = request.DoctorNotes?.Trim(),
            DietCompliance = string.IsNullOrWhiteSpace(request.DietCompliance)
                ? null
                : Enum.Parse<DietCompliance>(request.DietCompliance),
            SideEffects = request.SideEffects?.Trim(),
            NextWeekAdjustments = request.NextWeekAdjustments?.Trim(),
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = recordedBy
        };

        await _uow.WeeklyFollowUps.AddAsync(followUp);
        await _uow.SaveChangesAsync();

        // Administered items
        foreach (var item in request.AdministeredItems)
        {
            if (!item.PackageItemId.HasValue) continue;
            if (await _uow.PackageItems.ExistsAsync(item.PackageItemId.Value))
                await _uow.WeeklyAdministeredItems.AddAsync(new WeeklyAdministeredItem
                {
                    FollowUpId = followUp.Id,
                    PackageItemId = (int)item.PackageItemId,
                    ActualQuantity = (decimal)item.ActualQuantity,
                    AdministeredBy = recordedBy,
                    AdministeredAt = DateTime.UtcNow,
                    Notes = item.Notes?.Trim()
                });
        }

        // Lab results
        foreach (var lab in request.LabResults)
        {
            if (!lab.AnalysisTypeId.HasValue) continue;
            await _uow.WeeklyFollowUpLabResults.AddAsync(new WeeklyFollowUpLabResult
            {
                FollowUpId = followUp.Id,
                AnalysisTypeId = (int)lab.AnalysisTypeId,
                ResultValue = lab.ResultValue?.Trim(),
                ReferenceRange = lab.ReferenceRange?.Trim(),
                IsNormal = lab.IsNormal,
                Notes = lab.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _uow.SaveChangesAsync();

        // Auto-complete enrollment if all 4 weeks done
        if (nextWeek == 4)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            _uow.NutritionEnrollments.Update(enrollment);
            await _uow.SaveChangesAsync();
        }

        return ServiceResult<WeeklyFollowUpDto>.Success(
            await BuildFollowUpDtoAsync(followUp));
    }

    public async Task<ServiceResult<WeeklyFollowUpDto>> GetFollowUpByIdAsync(int followUpId)
    {
        var followUp = await _uow.WeeklyFollowUps.GetFollowUpWithDetailsAsync(followUpId);
        if (followUp is null)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Follow-up not found.");
        return ServiceResult<WeeklyFollowUpDto>.Success(await BuildFollowUpDtoAsync(followUp));
    }

    public async Task<ServiceResult<IEnumerable<WeeklyFollowUpDto>>> GetEnrollmentFollowUpsAsync(
        int enrollmentId)
    {
        var followUps = await _uow.WeeklyFollowUps.GetByEnrollmentAsync(enrollmentId);
        var dtos = new List<WeeklyFollowUpDto>();
        foreach (var f in followUps)
            dtos.Add(await BuildFollowUpDtoAsync(f));
        return ServiceResult<IEnumerable<WeeklyFollowUpDto>>.Success(dtos);
    }

    public async Task<ServiceResult> CompleteFollowUpAsync(int followUpId)
    {
        var followUp = await _uow.WeeklyFollowUps.GetByIdAsync(followUpId);
        if (followUp is null) return ServiceResult.Failure("Follow-up not found.");

        followUp.IsCompleted = true;
        followUp.CompletedAt = DateTime.UtcNow;
        _uow.WeeklyFollowUps.Update(followUp);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Follow-up marked as completed.");
    }
    // In NutritionService.cs

    public async Task<ServiceResult<WeeklyFollowUpDto>> UpdateFollowUpAsync(
        int followUpId, RecordFollowUpDto request, int updatedBy)
    {
        var followUp = await _uow.WeeklyFollowUps.GetByIdAsync(followUpId);
        if (followUp is null)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Follow-up not found.");

        // Update scalar properties
        followUp.FollowUpDate = request.FollowUpDate;
        followUp.WeightKg = request.WeightKg;
        followUp.HeightCm = request.HeightCm;
        followUp.BodyFatPercent = request.BodyFatPercent;
        followUp.MuscleMassKg = request.MuscleMassKg;
        followUp.WaistCircumferenceCm = request.WaistCircumferenceCm;
        followUp.BloodPressureSys = request.BloodPressureSys;
        followUp.BloodPressureDia = request.BloodPressureDia;
        followUp.LabResultsSummary = request.LabResultsSummary?.Trim();
        followUp.DoctorNotes = request.DoctorNotes?.Trim();
        followUp.DietCompliance = string.IsNullOrWhiteSpace(request.DietCompliance)
            ? null
            : Enum.Parse<DietCompliance>(request.DietCompliance);
        followUp.SideEffects = request.SideEffects?.Trim();
        followUp.NextWeekAdjustments = request.NextWeekAdjustments?.Trim();
        followUp.UpdatedAt = DateTime.UtcNow;
        followUp.UpdatedBy = updatedBy;

        _uow.WeeklyFollowUps.Update(followUp);
        await _uow.SaveChangesAsync();

        // Optional: Update administered items and lab results
        // (Delete existing and re-add, or implement merge logic)

        return ServiceResult<WeeklyFollowUpDto>.Success(
            await BuildFollowUpDtoAsync(followUp));
    }

    public async Task<ServiceResult> DeleteFollowUpAsync(int followUpId)
    {
        var followUp = await _uow.WeeklyFollowUps.GetByIdAsync(followUpId);
        if (followUp is null)
            return ServiceResult.Failure("Follow-up not found.");

        // Delete related records first
        var administeredItems = await _uow.WeeklyAdministeredItems
            .FindAsync(a => a.FollowUpId == followUpId);
        foreach (var item in administeredItems)
            _uow.WeeklyAdministeredItems.Delete(item);

        var labResults = await _uow.WeeklyFollowUpLabResults
            .FindAsync(l => l.FollowUpId == followUpId);
        foreach (var lab in labResults)
            _uow.WeeklyFollowUpLabResults.Delete(lab);

        _uow.WeeklyFollowUps.Delete(followUp);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success("Follow-up deleted.");
    }
    // ── Injection Type catalog ───────────────────────────────

    public async Task<ServiceResult<IEnumerable<InjectionTypeDto>>> GetInjectionTypesAsync(
        bool includeInactive = false)
    {
        var items = await _uow.InjectionTypes.GetAllAsync();
        var packageItems = await _uow.PackageItems.GetAllAsync();
        var usedIds = packageItems.Where(pi => pi.InjectionId.HasValue)
            .Select(pi => pi.InjectionId!.Value).ToHashSet();

        var result = items
            .Where(i => includeInactive || i.IsActive)
            .OrderBy(i => i.InjectionName)
            .Select(i => MapInjectionDto(i, usedIds.Contains(i.Id)));
        return ServiceResult<IEnumerable<InjectionTypeDto>>.Success(result);
    }

    public async Task<ServiceResult<InjectionTypeDto>> GetInjectionTypeByIdAsync(int id)
    {
        var item = await _uow.InjectionTypes.GetByIdAsync(id);
        if (item is null) return ServiceResult<InjectionTypeDto>.Failure("Injection type not found.");
        var inUse = (await _uow.PackageItems.FindAsync(pi => pi.InjectionId == id)).Any();
        return ServiceResult<InjectionTypeDto>.Success(MapInjectionDto(item, inUse));
    }

    public async Task<ServiceResult<InjectionTypeDto>> CreateInjectionTypeAsync(
        CreateInjectionTypeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.InjectionName))
            return ServiceResult<InjectionTypeDto>.Failure("Injection name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit))
            return ServiceResult<InjectionTypeDto>.Failure("Unit is required.");

        var duplicate = (await _uow.InjectionTypes.FindAsync(
            i => i.InjectionName.ToLower() == request.InjectionName.Trim().ToLower())).Any();
        if (duplicate)
            return ServiceResult<InjectionTypeDto>.Failure("An injection type with this name already exists.");

        var entity = new InjectionType
        {
            InjectionName = request.InjectionName.Trim(),
            Unit = request.Unit.Trim(),
            Description = request.Description?.Trim(),
            DefaultDosage = request.DefaultDosage?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.InjectionTypes.AddAsync(entity);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjectionTypeDto>.Success(MapInjectionDto(entity, false), "Injection type created.");
    }

    public async Task<ServiceResult<InjectionTypeDto>> UpdateInjectionTypeAsync(
        int id, UpdateInjectionTypeDto request)
    {
        var entity = await _uow.InjectionTypes.GetByIdAsync(id);
        if (entity is null) return ServiceResult<InjectionTypeDto>.Failure("Injection type not found.");
        if (string.IsNullOrWhiteSpace(request.InjectionName))
            return ServiceResult<InjectionTypeDto>.Failure("Injection name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit))
            return ServiceResult<InjectionTypeDto>.Failure("Unit is required.");

        entity.InjectionName = request.InjectionName.Trim();
        entity.Unit = request.Unit.Trim();
        entity.Description = request.Description?.Trim();
        entity.DefaultDosage = request.DefaultDosage?.Trim();
        entity.IsActive = request.IsActive;

        _uow.InjectionTypes.Update(entity);
        await _uow.SaveChangesAsync();

        var inUse = (await _uow.PackageItems.FindAsync(pi => pi.InjectionId == id)).Any();
        return ServiceResult<InjectionTypeDto>.Success(MapInjectionDto(entity, inUse), "Injection type updated.");
    }

    public async Task<ServiceResult> DeleteInjectionTypeAsync(int id)
    {
        var entity = await _uow.InjectionTypes.GetByIdAsync(id);
        if (entity is null) return ServiceResult.Failure("Injection type not found.");

        var inUse = (await _uow.PackageItems.FindAsync(pi => pi.InjectionId == id)).Any();
        if (inUse)
        {
            // Referenced by one or more nutrition packages — deactivate instead
            // of a hard delete so historical packages keep a valid name.
            entity.IsActive = false;
            _uow.InjectionTypes.Update(entity);
            await _uow.SaveChangesAsync();
            return ServiceResult.Success(
                "This injection type is used by existing nutrition packages, so it was deactivated instead of deleted.");
        }

        _uow.InjectionTypes.Delete(entity);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Injection type deleted.");
    }

    // ── Vitamin Type catalog ──────────────────────────────────

    public async Task<ServiceResult<IEnumerable<VitaminTypeDto>>> GetVitaminTypesAsync(
        bool includeInactive = false)
    {
        var items = await _uow.VitaminTypes.GetAllAsync();
        var packageItems = await _uow.PackageItems.GetAllAsync();
        var usedIds = packageItems.Where(pi => pi.VitaminId.HasValue)
            .Select(pi => pi.VitaminId!.Value).ToHashSet();

        var result = items
            .Where(v => includeInactive || v.IsActive)
            .OrderBy(v => v.VitaminName)
            .Select(v => MapVitaminDto(v, usedIds.Contains(v.Id)));
        return ServiceResult<IEnumerable<VitaminTypeDto>>.Success(result);
    }

    public async Task<ServiceResult<VitaminTypeDto>> GetVitaminTypeByIdAsync(int id)
    {
        var item = await _uow.VitaminTypes.GetByIdAsync(id);
        if (item is null) return ServiceResult<VitaminTypeDto>.Failure("Vitamin type not found.");
        var inUse = (await _uow.PackageItems.FindAsync(pi => pi.VitaminId == id)).Any();
        return ServiceResult<VitaminTypeDto>.Success(MapVitaminDto(item, inUse));
    }

    public async Task<ServiceResult<VitaminTypeDto>> CreateVitaminTypeAsync(
        CreateVitaminTypeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.VitaminName))
            return ServiceResult<VitaminTypeDto>.Failure("Vitamin name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit))
            return ServiceResult<VitaminTypeDto>.Failure("Unit is required.");

        var duplicate = (await _uow.VitaminTypes.FindAsync(
            v => v.VitaminName.ToLower() == request.VitaminName.Trim().ToLower())).Any();
        if (duplicate)
            return ServiceResult<VitaminTypeDto>.Failure("A vitamin type with this name already exists.");

        var entity = new VitaminType
        {
            VitaminName = request.VitaminName.Trim(),
            Formulation = request.Formulation?.Trim(),
            Unit = request.Unit.Trim(),
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.VitaminTypes.AddAsync(entity);
        await _uow.SaveChangesAsync();
        return ServiceResult<VitaminTypeDto>.Success(MapVitaminDto(entity, false), "Vitamin type created.");
    }

    public async Task<ServiceResult<VitaminTypeDto>> UpdateVitaminTypeAsync(
        int id, UpdateVitaminTypeDto request)
    {
        var entity = await _uow.VitaminTypes.GetByIdAsync(id);
        if (entity is null) return ServiceResult<VitaminTypeDto>.Failure("Vitamin type not found.");
        if (string.IsNullOrWhiteSpace(request.VitaminName))
            return ServiceResult<VitaminTypeDto>.Failure("Vitamin name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit))
            return ServiceResult<VitaminTypeDto>.Failure("Unit is required.");

        entity.VitaminName = request.VitaminName.Trim();
        entity.Formulation = request.Formulation?.Trim();
        entity.Unit = request.Unit.Trim();
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;

        _uow.VitaminTypes.Update(entity);
        await _uow.SaveChangesAsync();

        var inUse = (await _uow.PackageItems.FindAsync(pi => pi.VitaminId == id)).Any();
        return ServiceResult<VitaminTypeDto>.Success(MapVitaminDto(entity, inUse), "Vitamin type updated.");
    }

    public async Task<ServiceResult> DeleteVitaminTypeAsync(int id)
    {
        var entity = await _uow.VitaminTypes.GetByIdAsync(id);
        if (entity is null) return ServiceResult.Failure("Vitamin type not found.");

        var inUse = (await _uow.PackageItems.FindAsync(pi => pi.VitaminId == id)).Any();
        if (inUse)
        {
            entity.IsActive = false;
            _uow.VitaminTypes.Update(entity);
            await _uow.SaveChangesAsync();
            return ServiceResult.Success(
                "This vitamin type is used by existing nutrition packages, so it was deactivated instead of deleted.");
        }

        _uow.VitaminTypes.Delete(entity);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Vitamin type deleted.");
    }

    private static InjectionTypeDto MapInjectionDto(InjectionType i, bool isInUse) => new()
    {
        Id = i.Id,
        InjectionName = i.InjectionName,
        Unit = i.Unit,
        Description = i.Description,
        DefaultDosage = i.DefaultDosage,
        IsActive = i.IsActive,
        IsInUse = isInUse
    };

    private static VitaminTypeDto MapVitaminDto(VitaminType v, bool isInUse) => new()
    {
        Id = v.Id,
        VitaminName = v.VitaminName,
        Formulation = v.Formulation,
        Unit = v.Unit,
        Description = v.Description,
        IsActive = v.IsActive,
        IsInUse = isInUse
    };

    // ── Mappers ──────────────────────────────────────────────

    private static NutritionPackageDto MapPackageDto(NutritionPackage p)
        => new()
        {
            Id = p.Id,
            PackageName = p.PackageName,
            Description = p.Description,
            DurationWeeks = p.DurationWeeks,
            SessionsPerWeek = p.SessionsPerWeek,
            BasePrice = p.BasePrice,
            MaxDiscountPercent = p.MaxDiscountPercent,
            IsActive = p.IsActive,
            Items = p.Items.Select(i => new PackageItemDto
            {
                Id = i.Id,
                InjectionId = i.InjectionId,
                InjectionName = i.Injection?.InjectionName,
                VitaminId = i.VitaminId,
                VitaminName = i.Vitamin?.VitaminName,
                Quantity = i.Quantity,
                Unit = i.Unit,
                WeekNumber = i.WeekNumber,
                Notes = i.Notes
            }).ToList()
        };

    private async Task<NutritionPackageDto> BuildPackageDtoAsync(int packageId)
    {
        var p = await _uow.NutritionPackages.GetPackageWithItemsAsync(packageId);
        return MapPackageDto(p!);
    }

    private async Task<PatientEnrollmentDto> BuildEnrollmentDtoAsync(PatientNutritionEnrollment e)
    {
        var patient = await _uow.Patients.GetByIdAsync(e.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(e.DoctorId);
        var package = await _uow.NutritionPackages.GetByIdAsync(e.PackageId);
        var followUps = await _uow.WeeklyFollowUps.GetByEnrollmentAsync(e.Id);

        var followUpDtos = new List<WeeklyFollowUpDto>();
        foreach (var f in followUps)
            followUpDtos.Add(await BuildFollowUpDtoAsync(f));

        return new PatientEnrollmentDto
        {
            Id = e.Id,
            PatientId = e.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            PackageId = e.PackageId,
            PackageName = package?.PackageName ?? "",
            DoctorId = e.DoctorId,
            DoctorName = doctor?.FullName ?? "",
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            BasePrice = e.BasePrice,
            DiscountPercent = e.DiscountPercent,
            FinalPrice = e.FinalPrice,
            Status = e.Status.ToString(),
            TotalPaid = e.TotalPaid,
            WeeklyFollowUps = followUpDtos
        };
    }

    private async Task<WeeklyFollowUpDto> BuildFollowUpDtoAsync(WeeklyFollowUp f)
    {
        var administeredItems = f.AdministeredItems.Any()
            ? f.AdministeredItems
            : await _uow.WeeklyAdministeredItems.FindAsync(a => a.FollowUpId == f.Id);

        var labResults = f.LabResults.Any()
            ? f.LabResults
            : await _uow.WeeklyFollowUpLabResults.FindAsync(l => l.FollowUpId == f.Id);

        var administeredDtos = new List<AdministeredItemDto>();
        foreach (var a in administeredItems)
        {
            var packageItem = await _uow.PackageItems.GetByIdAsync(a.PackageItemId);
            var administerer = await _uow.Users.GetByIdAsync(a.AdministeredBy);
            string itemName = "";
            if (packageItem?.InjectionId.HasValue == true)
            {
                var inj = await _uow.InjectionTypes.GetByIdAsync(packageItem.InjectionId.Value);
                itemName = inj?.InjectionName ?? "";
            }
            else if (packageItem?.VitaminId.HasValue == true)
            {
                var vit = await _uow.VitaminTypes.GetByIdAsync(packageItem.VitaminId.Value);
                itemName = vit?.VitaminName ?? "";
            }

            administeredDtos.Add(new AdministeredItemDto
            {
                Id = a.Id,
                ItemName = itemName,
                ActualQuantity = a.ActualQuantity,
                AdministeredByName = administerer?.FullName,
                AdministeredAt = a.AdministeredAt
            });
        }

        var labDtos = new List<LabResultDto>();
        foreach (var l in labResults)
        {
            var aType = await _uow.AnalysisTypes.GetByIdAsync(l.AnalysisTypeId);
            labDtos.Add(new LabResultDto
            {
                Id = l.Id,
                AnalysisTypeName = aType?.TypeName ?? "",
                ResultValue = l.ResultValue,
                ReferenceRange = l.ReferenceRange,
                IsNormal = l.IsNormal
            });
        }

        return new WeeklyFollowUpDto
        {
            Id = f.Id,
            EnrollmentId = f.EnrollmentId,
            WeekNumber = f.WeekNumber,
            FollowUpDate = f.FollowUpDate,
            WeightKg = f.WeightKg,
            BMI = f.BMI,
            BodyFatPercent = f.BodyFatPercent,
            MuscleMassKg = f.MuscleMassKg,
            WaistCircumferenceCm = f.WaistCircumferenceCm,
            BloodPressure = (f.BloodPressureSys.HasValue && f.BloodPressureDia.HasValue)
                ? $"{f.BloodPressureSys}/{f.BloodPressureDia}"
                : null,
            LabResultsSummary = f.LabResultsSummary,
            DoctorNotes = f.DoctorNotes,
            DietCompliance = f.DietCompliance?.ToString(),
            SideEffects = f.SideEffects,
            NextWeekAdjustments = f.NextWeekAdjustments,
            IsCompleted = f.IsCompleted,
            AdministeredItems = administeredDtos,
            LabResults = labDtos
        };
    }
}