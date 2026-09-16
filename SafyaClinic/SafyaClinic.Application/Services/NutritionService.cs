using Microsoft.EntityFrameworkCore;
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
        // Validate package items
        foreach (var item in request.Items)
        {
            if (item.InjectionId is null && item.VitaminId is null)
                return ServiceResult<NutritionPackageDto>.Failure(
                    "Each package item must have either an injection or a vitamin.");

            if (item.InjectionId.HasValue && item.VitaminId.HasValue)
                return ServiceResult<NutritionPackageDto>.Failure(
                    "Each package item must have either an injection or a vitamin, not both.");

            if (item.WeekNumber < 1 || item.WeekNumber > 4)
                return ServiceResult<NutritionPackageDto>.Failure(
                    "Week number must be between 1 and 4.");
        }

        var injectionIds = request.Items
            .Where(i => i.InjectionId.HasValue)
            .Select(i => i.InjectionId!.Value)
            .Distinct()
            .ToList();

        var vitaminIds = request.Items
            .Where(i => i.VitaminId.HasValue)
            .Select(i => i.VitaminId!.Value)
            .Distinct()
            .ToList();

        if (injectionIds.Count > 0)
        {
            var existingInjectionIds = await _uow.InjectionTypes
                .Query()
                .Where(i => injectionIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync();

            var missingInjectionId = injectionIds
                .FirstOrDefault(id => !existingInjectionIds.Contains(id));

            if (missingInjectionId != 0)
                return ServiceResult<NutritionPackageDto>.Failure(
                    $"Injection type ID {missingInjectionId} not found.");
        }

        if (vitaminIds.Count > 0)
        {
            var existingVitaminIds = await _uow.VitaminTypes
                .Query()
                .Where(v => vitaminIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToListAsync();

            var missingVitaminId = vitaminIds
                .FirstOrDefault(id => !existingVitaminIds.Contains(id));

            if (missingVitaminId != 0)
                return ServiceResult<NutritionPackageDto>.Failure(
                    $"Vitamin type ID {missingVitaminId} not found.");
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

        var packageItems = request.Items
            .Select(item => new PackageItem
            {
                PackageId = package.Id,
                InjectionId = item.InjectionId,
                VitaminId = item.VitaminId,
                Quantity = item.Quantity,
                Unit = item.Unit?.Trim(),
                WeekNumber = item.WeekNumber,
                Notes = item.Notes?.Trim()
            })
            .ToList();

        await _uow.PackageItems.AddRangeAsync(packageItems);

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
        var existing = await _uow.NutritionEnrollments
            .Query()
            .Where(e => e.PatientId == request.PatientId && e.Status == EnrollmentStatus.Active)
            .AnyAsync();
        if (existing)
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

        return await GetEnrollmentByIdAsync(enrollment.Id);
    }

    public async Task<ServiceResult<PatientEnrollmentDto>> GetEnrollmentByIdAsync(
    int enrollmentId)
    {
        var enrollment = await _uow.NutritionEnrollments
            .Query()
            .AsSplitQuery()
            .Include(e => e.Patient)
            .Include(e => e.Package)
            .Include(e => e.Doctor)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.AdministeredItems)
                    .ThenInclude(a => a.PackageItem)
                        .ThenInclude(pi => pi.Injection)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.AdministeredItems)
                    .ThenInclude(a => a.PackageItem)
                        .ThenInclude(pi => pi.Vitamin)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.AdministeredItems)
                    .ThenInclude(a => a.AdministerByUser)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.LabResults)
                    .ThenInclude(l => l.AnalysisType)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId);

        if (enrollment is null)
            return ServiceResult<PatientEnrollmentDto>.Failure("Enrollment not found.");

        return ServiceResult<PatientEnrollmentDto>.Success(
            await BuildEnrollmentDtoAsync(enrollment));
    }

    public async Task<ServiceResult<IEnumerable<PatientEnrollmentDto>>> GetPatientEnrollmentsAsync(
    int patientId)
    {
        var enrollments = await _uow.NutritionEnrollments
            .Query()
            .AsSplitQuery()
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.StartDate)
            .Include(e => e.Patient)
            .Include(e => e.Package)
            .Include(e => e.Doctor)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.AdministeredItems)
                    .ThenInclude(a => a.PackageItem)
                        .ThenInclude(pi => pi.Injection)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.AdministeredItems)
                    .ThenInclude(a => a.PackageItem)
                        .ThenInclude(pi => pi.Vitamin)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.AdministeredItems)
                    .ThenInclude(a => a.AdministerByUser)
            .Include(e => e.WeeklyFollowUps)
                .ThenInclude(f => f.LabResults)
                    .ThenInclude(l => l.AnalysisType)
            .ToListAsync();

        var dtos = new List<PatientEnrollmentDto>();

        foreach (var enrollment in enrollments)
            dtos.Add(await BuildEnrollmentDtoAsync(enrollment));

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
        var maxWeekNumber = await _uow.WeeklyFollowUps
            .GetMaxWeekNumberAsync(enrollmentId);

        var nextWeek = (maxWeekNumber ?? 0) + 1;

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

        var packageItemIds = request.AdministeredItems
            .Where(i => i.PackageItemId.HasValue)
            .Select(i => i.PackageItemId!.Value)
            .Distinct()
            .ToList();

        var existingPackageItemIds = packageItemIds.Count == 0
            ? new HashSet<int>()
            : (await _uow.PackageItems
                .Query()
                .Where(p => packageItemIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync())
                .ToHashSet();

        var administeredItems = request.AdministeredItems
                .Where(item => item.PackageItemId.HasValue &&
                            item.ActualQuantity.HasValue &&
                            existingPackageItemIds.Contains(item.PackageItemId.Value))
                .Select(item => new WeeklyAdministeredItem
                {
                    FollowUpId = followUp.Id,
                    PackageItemId = item.PackageItemId!.Value,
                    ActualQuantity = item.ActualQuantity!.Value,
                    AdministeredBy = recordedBy,
                    AdministeredAt = DateTime.UtcNow,
                    Notes = item.Notes?.Trim()
                })
                .ToList();

        await _uow.WeeklyAdministeredItems.AddRangeAsync(administeredItems);

        // Lab results

        var labResults = request.LabResults
                .Where(lab => lab.AnalysisTypeId.HasValue)
                .Select(lab => new WeeklyFollowUpLabResult
                {
                    FollowUpId = followUp.Id,
                    AnalysisTypeId = lab.AnalysisTypeId!.Value,
                    ResultValue = lab.ResultValue?.Trim(),
                    ReferenceRange = lab.ReferenceRange?.Trim(),
                    IsNormal = lab.IsNormal,
                    Notes = lab.Notes?.Trim(),
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

        await _uow.WeeklyFollowUpLabResults.AddRangeAsync(labResults);

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
        _uow.WeeklyAdministeredItems.DeleteRange(administeredItems);

        var labResults = await _uow.WeeklyFollowUpLabResults
            .FindAsync(l => l.FollowUpId == followUpId);
        _uow.WeeklyFollowUpLabResults.DeleteRange(labResults);

        _uow.WeeklyFollowUps.Delete(followUp);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success("Follow-up deleted.");
    }
    // ── Injection Type catalog ───────────────────────────────

    public async Task<ServiceResult<IEnumerable<InjectionTypeDto>>> GetInjectionTypesAsync(
        bool includeInactive = false)
    {
        var items = await _uow.InjectionTypes.GetAllAsync();
        var usedInjectionIds = await _uow.PackageItems
                .Query()
                .Where(pi => pi.InjectionId.HasValue)
                .Select(pi => pi.InjectionId!.Value)
                .Distinct()
                .ToListAsync();

        var usedIds = usedInjectionIds.ToHashSet();

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
        var inUse = await _uow.PackageItems
            .Query()
            .Where(pi => pi.InjectionId == id)
            .AnyAsync();
        return ServiceResult<InjectionTypeDto>.Success(MapInjectionDto(item, inUse));
    }

    public async Task<ServiceResult<InjectionTypeDto>> CreateInjectionTypeAsync(
        CreateInjectionTypeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.InjectionName))
            return ServiceResult<InjectionTypeDto>.Failure("Injection name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit))
            return ServiceResult<InjectionTypeDto>.Failure("Unit is required.");

        var duplicate = await _uow.InjectionTypes
            .Query()
            .Where(i => i.InjectionName.ToLower() == request.InjectionName.Trim().ToLower())
            .AnyAsync();
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

        var inUse = await _uow.PackageItems
            .Query()
            .Where(pi => pi.InjectionId == id)
            .AnyAsync();
        return ServiceResult<InjectionTypeDto>.Success(MapInjectionDto(entity, inUse), "Injection type updated.");
    }

    public async Task<ServiceResult> DeleteInjectionTypeAsync(int id)
    {
        var entity = await _uow.InjectionTypes.GetByIdAsync(id);
        if (entity is null) return ServiceResult.Failure("Injection type not found.");

        var inUse = await _uow.PackageItems
            .Query()
            .Where(pi => pi.InjectionId == id)
            .AnyAsync();
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
        var usedVitaminIds = await _uow.PackageItems
                .Query()
                .Where(pi => pi.VitaminId.HasValue)
                .Select(pi => pi.VitaminId!.Value)
                .Distinct()
                .ToListAsync();

        var usedIds = usedVitaminIds.ToHashSet();

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
        var inUse = await _uow.PackageItems
            .Query()
            .Where(pi => pi.VitaminId == id)
            .AnyAsync();
        return ServiceResult<VitaminTypeDto>.Success(MapVitaminDto(item, inUse));
    }

    public async Task<ServiceResult<VitaminTypeDto>> CreateVitaminTypeAsync(
        CreateVitaminTypeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.VitaminName))
            return ServiceResult<VitaminTypeDto>.Failure("Vitamin name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit))
            return ServiceResult<VitaminTypeDto>.Failure("Unit is required.");

        var duplicate = await _uow.VitaminTypes
            .Query()
            .Where(v => v.VitaminName.ToLower() == request.VitaminName.Trim().ToLower())
            .AnyAsync();
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

        var inUse = await _uow.PackageItems
            .Query()
            .Where(pi => pi.VitaminId == id)
            .AnyAsync();
        return ServiceResult<VitaminTypeDto>.Success(MapVitaminDto(entity, inUse), "Vitamin type updated.");
    }

    public async Task<ServiceResult> DeleteVitaminTypeAsync(int id)
    {
        var entity = await _uow.VitaminTypes.GetByIdAsync(id);
        if (entity is null) return ServiceResult.Failure("Vitamin type not found.");

        var inUse = await _uow.PackageItems
            .Query()
            .Where(pi => pi.VitaminId == id)
            .AnyAsync();
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

    private async Task<PatientEnrollmentDto> BuildEnrollmentDtoAsync(
    PatientNutritionEnrollment e)
    {
        var followUpDtos = new List<WeeklyFollowUpDto>();

        foreach (var f in e.WeeklyFollowUps.OrderBy(f => f.WeekNumber))
            followUpDtos.Add(await BuildFollowUpDtoAsync(f));

        return new PatientEnrollmentDto
        {
            Id = e.Id,
            PatientId = e.PatientId,
            PatientName = e.Patient is null
                ? ""
                : $"{e.Patient.FirstName} {e.Patient.LastName}",

            PackageId = e.PackageId,
            PackageName = e.Package?.PackageName ?? "",

            DoctorId = e.DoctorId,
            DoctorName = e.Doctor?.FullName ?? "",

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
            : await _uow.WeeklyFollowUps
                .GetAdministeredItemsWithDetailsAsync(f.Id);

        var labResults = f.LabResults.Any()
            ? f.LabResults
            : await _uow.WeeklyFollowUps
                .GetLabResultsWithDetailsAsync(f.Id);

        var administeredDtos = new List<AdministeredItemDto>();
        foreach (var a in administeredItems)
        {
            var packageItem = a.PackageItem;
            var administerer = a.AdministerByUser;
            string itemName = "";
            if (packageItem?.InjectionId.HasValue == true)
            {
                itemName = packageItem.Injection?.InjectionName ?? "";
            }
            else if (packageItem?.VitaminId.HasValue == true)
            {
                itemName = packageItem.Vitamin?.VitaminName ?? "";
            }

            administeredDtos.Add(new AdministeredItemDto
            {
                Id = a.Id,
                ItemName = itemName,
                ActualQuantity = a.ActualQuantity,
                AdministeredByName = administerer?.FullName ?? "",
                AdministeredAt = a.AdministeredAt
            });
        }

        var labDtos = new List<LabResultDto>();
        foreach (var l in labResults)
        {
            var aType = l.AnalysisType;
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