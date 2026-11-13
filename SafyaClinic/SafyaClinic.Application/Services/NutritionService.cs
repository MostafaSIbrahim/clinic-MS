using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
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
    private readonly IAuditTrail _audit;

    public NutritionService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit ?? NoAuditTrail.Instance;
    }

    // ── Packages ─────────────────────────────────────────────

    public async Task<ServiceResult<NutritionPackageDto>> CreatePackageAsync(
        CreateNutritionPackageDto request, int createdBy)
    {
        if (string.IsNullOrWhiteSpace(request.PackageName))
            return ServiceResult<NutritionPackageDto>.Failure("Enter the package name.");   // M4c
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

            if (item.Quantity <= 0)   // M4c
                return ServiceResult<NutritionPackageDto>.Failure("Each item needs a quantity above zero.");
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

        var dto = await BuildEnrollmentDtoAsync(enrollment);
        // M4d: the patient's plan for the weeks not recorded yet.
        var latest = enrollment.WeeklyFollowUps.Count == 0 ? 0 : enrollment.WeeklyFollowUps.Max(f => f.WeekNumber);
        var planned = await _uow.EnrollmentPlannedItems.Query()
            .Where(p => p.EnrollmentId == enrollmentId && p.WeekNumber > latest)
            .OrderBy(p => p.WeekNumber).ThenBy(p => p.Id)
            .ToListAsync();
        if (planned.Count > 0)
        {
            var names = await LoadItemNamesAsync(enrollment.PackageId);
            dto.PlannedItems = planned.Select(p => Line(names, p.WeekNumber, p.PackageItemId, p.InjectionId, p.VitaminId,
                p.CustomName, p.Quantity, p.Unit, p.Notes, false)).ToList();
        }
        return ServiceResult<PatientEnrollmentDto>.Success(dto);
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
        if (!Enum.TryParse<EnrollmentStatus>(status, out var target) || !Enum.IsDefined(target))
            return ServiceResult.Failure("Choose a valid enrollment status.");

        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null) return ServiceResult.Failure("Enrollment not found.");

        // M4a: the status rules (EnrollmentRules), enforced here so every screen and request follows them.
        var current = enrollment.Status;
        if (current == target) return ServiceResult.Success("No change.");
        var problem = EnrollmentRules.CheckStatusChange(current, target);
        if (problem is not null) return ServiceResult.Failure(problem);

        // One active enrollment per patient, as when enrolling: resuming must not create a second one.
        if (target == EnrollmentStatus.Active && await _uow.NutritionEnrollments.Query()
                .AnyAsync(e => e.PatientId == enrollment.PatientId && e.Id != enrollment.Id && e.Status == EnrollmentStatus.Active))
            return ServiceResult.Failure(EnrollmentRules.AnotherActiveMessage);

        enrollment.Status = target;
        enrollment.UpdatedAt = DateTime.UtcNow;
        _uow.NutritionEnrollments.Update(enrollment);
        await _audit.RecordAsync(AuditActions.EnrollmentStatusChanged, "NutritionEnrollment", enrollment.Id,
            new { from = current.ToString(), to = target.ToString() });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success(target switch
        {
            EnrollmentStatus.OnHold => "Enrollment put on hold.",
            EnrollmentStatus.Active => "Enrollment resumed.",
            EnrollmentStatus.Cancelled => "Enrollment cancelled.",
            _ => "Enrollment status updated.",
        });
    }

    // ── Weekly Follow-Ups ────────────────────────────────────

    /// <summary>M4b: the most weekly follow-ups an enrollment has (packages run four weeks).</summary>
    public const int FollowUpWeeks = 4;

    public async Task<ServiceResult<WeeklyFollowUpDto>> RecordFollowUpAsync(
        int enrollmentId, RecordFollowUpDto request, int recordedBy)
    {
        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Enrollment not found.");
        if (enrollment.Status != EnrollmentStatus.Active)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Enrollment is not active.");

        // Weeks are numbered 1, 2, 3, 4 with no gaps: only the latest week can be deleted (M4b).
        var maxWeekNumber = await _uow.WeeklyFollowUps.GetMaxWeekNumberAsync(enrollmentId);
        var nextWeek = (maxWeekNumber ?? 0) + 1;
        if (nextWeek > FollowUpWeeks)
            return ServiceResult<WeeklyFollowUpDto>.Failure(
                "All 4 weekly follow-ups are already recorded for this enrollment.");

        var checkedInput = await CheckFollowUpInputAsync(enrollment, request, nextWeek + 1);
        if (checkedInput.Error is not null)
            return ServiceResult<WeeklyFollowUpDto>.Failure(checkedInput.Error);

        var followUp = new WeeklyFollowUp
        {
            EnrollmentId = enrollmentId,
            WeekNumber = nextWeek,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = recordedBy
        };
        ApplyFollowUpFields(followUp, request, checkedInput.Diet);

        await _uow.WeeklyFollowUps.AddAsync(followUp);
        await _uow.SaveChangesAsync();

        await AddFollowUpItemsAsync(followUp.Id, request, recordedBy);
        await SavePlanAsync(enrollmentId, request, nextWeek + 1, recordedBy);   // M4d
        await _uow.SaveChangesAsync();

        // Auto-complete the enrollment when its last week is recorded.
        if (nextWeek == FollowUpWeeks)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            _uow.NutritionEnrollments.Update(enrollment);
            await _uow.SaveChangesAsync();
        }

        return ServiceResult<WeeklyFollowUpDto>.Success(
            await BuildFollowUpDtoAsync(followUp));
    }

    /// <summary>
    /// Checks a follow-up form (M4b), including items outside the package and the plan for the coming weeks
    /// (M4d). Each given or planned line names exactly one item: a package item of this enrollment, an injection
    /// type, a vitamin type, or a drug typed by name, with a quantity above zero. Lab results name a known analysis.
    /// </summary>
    private async Task<(string? Error, DietCompliance? Diet)> CheckFollowUpInputAsync(
        PatientNutritionEnrollment enrollment, RecordFollowUpDto request, int firstPlanWeek)
    {
        if (request.FollowUpDate == default)
            return ("Enter the follow-up date.", null);

        DietCompliance? diet = null;
        if (!string.IsNullOrWhiteSpace(request.DietCompliance))
        {
            if (!Enum.TryParse<DietCompliance>(request.DietCompliance, out var parsed) || !Enum.IsDefined(parsed))
                return ("Choose a valid diet compliance.", null);
            diet = parsed;
        }

        var given = request.AdministeredItems.Where(i => i.Given).ToList();
        var lines = given.Select(i => (PackageItemId: i.PackageItemId, InjectionId: i.InjectionId, VitaminId: i.VitaminId, CustomName: i.CustomName, Quantity: i.ActualQuantity))
            .Concat(request.PlannedItems.Select(p => (PackageItemId: p.PackageItemId, InjectionId: p.InjectionId, VitaminId: p.VitaminId, CustomName: p.CustomName, Quantity: p.Quantity)))
            .ToList();
        foreach (var (packageItemId, injectionId, vitaminId, customName, quantity) in lines)
        {
            var named = (packageItemId.HasValue ? 1 : 0) + (injectionId.HasValue ? 1 : 0) + (vitaminId.HasValue ? 1 : 0)
                + (string.IsNullOrWhiteSpace(customName) ? 0 : 1);
            if (named != 1)
                return ("Each item must be one package item, injection, vitamin or drug name.", null);
            if (customName is { Length: > 200 })
                return ("A drug name can be at most 200 characters.", null);
            if (quantity is null || quantity <= 0)
                return ("Enter a quantity above zero for each item given or planned.", null);
        }

        var givenPackageIds = given.Where(i => i.PackageItemId.HasValue).Select(i => i.PackageItemId!.Value).ToList();
        if (givenPackageIds.Count != givenPackageIds.Distinct().Count())
            return ("Each package item can be recorded once per follow-up.", null);

        var packageIds = lines.Where(l => l.PackageItemId.HasValue).Select(l => l.PackageItemId!.Value).Distinct().ToList();
        if (packageIds.Count > 0)
        {
            var found = await _uow.PackageItems.Query()
                .CountAsync(p => p.PackageId == enrollment.PackageId && packageIds.Contains(p.Id));
            if (found != packageIds.Count)
                return ("An item isn't part of this enrollment's package.", null);
        }
        var injectionIds = lines.Where(l => l.InjectionId.HasValue).Select(l => l.InjectionId!.Value).Distinct().ToList();
        if (injectionIds.Count > 0 && await _uow.InjectionTypes.Query().CountAsync(i => injectionIds.Contains(i.Id)) != injectionIds.Count)
            return ("An injection doesn't exist.", null);
        var vitaminIds = lines.Where(l => l.VitaminId.HasValue).Select(l => l.VitaminId!.Value).Distinct().ToList();
        if (vitaminIds.Count > 0 && await _uow.VitaminTypes.Query().CountAsync(v => vitaminIds.Contains(v.Id)) != vitaminIds.Count)
            return ("A vitamin doesn't exist.", null);

        if (request.PlannedItems.Any(p => p.WeekNumber < firstPlanWeek || p.WeekNumber > FollowUpWeeks))
            return ("Items can only be planned for the weeks still to come.", null);

        var analysisIds = request.LabResults
            .Where(l => l.AnalysisTypeId.HasValue)
            .Select(l => l.AnalysisTypeId!.Value)
            .Distinct()
            .ToList();
        if (analysisIds.Count > 0)
        {
            var known = await _uow.AnalysisTypes.Query().CountAsync(a => analysisIds.Contains(a.Id));
            if (known != analysisIds.Count)
                return ("A lab result names an analysis that doesn't exist.", null);
        }
        if (request.LabResults.Any(l => !l.AnalysisTypeId.HasValue
                && (!string.IsNullOrWhiteSpace(l.ResultValue) || !string.IsNullOrWhiteSpace(l.ReferenceRange))))
            return ("Choose the analysis for each lab result.", null);

        return (null, diet);
    }

    private static string? CleanName(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static void ApplyFollowUpFields(WeeklyFollowUp followUp, RecordFollowUpDto request, DietCompliance? diet)
    {
        followUp.FollowUpDate = request.FollowUpDate.Date;
        followUp.WeightKg = request.WeightKg;
        followUp.HeightCm = request.HeightCm;
        followUp.BodyFatPercent = request.BodyFatPercent;
        followUp.MuscleMassKg = request.MuscleMassKg;
        followUp.WaistCircumferenceCm = request.WaistCircumferenceCm;
        followUp.BloodPressureSys = request.BloodPressureSys;
        followUp.BloodPressureDia = request.BloodPressureDia;
        followUp.LabResultsSummary = request.LabResultsSummary?.Trim();
        followUp.DoctorNotes = request.DoctorNotes?.Trim();
        followUp.DietCompliance = diet;
        followUp.SideEffects = request.SideEffects?.Trim();
        followUp.NextWeekAdjustments = request.NextWeekAdjustments?.Trim();
    }

    private async Task AddFollowUpItemsAsync(int followUpId, RecordFollowUpDto request, int recordedBy)
    {
        await _uow.WeeklyAdministeredItems.AddRangeAsync(request.AdministeredItems
            .Where(item => item.Given && item.ActualQuantity.HasValue)
            .Select(item => new WeeklyAdministeredItem
            {
                FollowUpId = followUpId,
                PackageItemId = item.PackageItemId,
                InjectionId = item.InjectionId,
                VitaminId = item.VitaminId,
                CustomName = CleanName(item.CustomName),
                Unit = CleanName(item.Unit),
                ActualQuantity = item.ActualQuantity!.Value,
                AdministeredBy = recordedBy,
                AdministeredAt = DateTime.UtcNow,
                Notes = item.Notes?.Trim()
            })
            .ToList());

        await _uow.WeeklyFollowUpLabResults.AddRangeAsync(request.LabResults
            .Where(lab => lab.AnalysisTypeId.HasValue)
            .Select(lab => new WeeklyFollowUpLabResult
            {
                FollowUpId = followUpId,
                AnalysisTypeId = lab.AnalysisTypeId!.Value,
                ResultValue = lab.ResultValue?.Trim(),
                ReferenceRange = lab.ReferenceRange?.Trim(),
                IsNormal = lab.IsNormal,
                Notes = lab.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            })
            .ToList());
    }

    /// <summary>
    /// M4d: replaces the patient's plan for weeks <paramref name="firstPlanWeek"/>–4 with the form's lines. Weeks
    /// already recorded keep their plan as history. The package itself never changes.
    /// </summary>
    private async Task SavePlanAsync(int enrollmentId, RecordFollowUpDto request, int firstPlanWeek, int userId)
    {
        if (firstPlanWeek > FollowUpWeeks) return;
        _uow.EnrollmentPlannedItems.DeleteRange(await _uow.EnrollmentPlannedItems
            .FindAsync(p => p.EnrollmentId == enrollmentId && p.WeekNumber >= firstPlanWeek));
        await _uow.EnrollmentPlannedItems.AddRangeAsync(request.PlannedItems.Select(p => new EnrollmentPlannedItem
        {
            EnrollmentId = enrollmentId,
            WeekNumber = p.WeekNumber,
            PackageItemId = p.PackageItemId,
            InjectionId = p.InjectionId,
            VitaminId = p.VitaminId,
            CustomName = CleanName(p.CustomName),
            Quantity = p.Quantity!.Value,
            Unit = CleanName(p.Unit),
            Notes = p.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
        }).ToList());
    }

    /// <summary>Names and units for package items, injections and vitamins, for display.</summary>
    private sealed record ItemNames(
        Dictionary<int, (string Name, string Kind, string? Unit, int Week, decimal Quantity, string? Notes)> Package,
        Dictionary<int, (string Name, string Unit)> Injections,
        Dictionary<int, (string Name, string Unit)> Vitamins);

    private async Task<ItemNames> LoadItemNamesAsync(int packageId)
    {
        var package = await _uow.PackageItems.Query()
            .Where(p => p.PackageId == packageId)
            .Select(p => new
            {
                p.Id, p.WeekNumber, p.Quantity, p.Unit, p.Notes,
                InjectionName = p.Injection != null ? p.Injection.InjectionName : null,
                InjectionUnit = p.Injection != null ? p.Injection.Unit : null,
                VitaminName = p.Vitamin != null ? p.Vitamin.VitaminName : null,
                VitaminUnit = p.Vitamin != null ? p.Vitamin.Unit : null,
            })
            .ToListAsync();
        var injections = await _uow.InjectionTypes.Query().Select(i => new { i.Id, i.InjectionName, i.Unit }).ToListAsync();
        var vitamins = await _uow.VitaminTypes.Query().Select(v => new { v.Id, v.VitaminName, v.Unit }).ToListAsync();
        return new ItemNames(
            package.ToDictionary(p => p.Id, p => (
                p.InjectionName ?? p.VitaminName ?? "—",
                p.InjectionName != null ? "Injection" : "Vitamin",
                p.Unit ?? p.InjectionUnit ?? p.VitaminUnit,
                p.WeekNumber, p.Quantity, p.Notes)),
            injections.ToDictionary(i => i.Id, i => (i.InjectionName, i.Unit)),
            vitamins.ToDictionary(v => v.Id, v => (v.VitaminName, v.Unit)));
    }

    private static FollowUpLineDto Line(ItemNames names, int week, int? packageItemId, int? injectionId, int? vitaminId,
        string? customName, decimal quantity, string? unit, string? notes, bool given)
    {
        var line = new FollowUpLineDto
        {
            WeekNumber = week, PackageItemId = packageItemId, InjectionId = injectionId, VitaminId = vitaminId,
            CustomName = customName, Quantity = quantity, Unit = unit, Notes = notes, Given = given,
        };
        if (packageItemId is int p && names.Package.TryGetValue(p, out var pi))
        {
            line.Name = pi.Name; line.Kind = pi.Kind; line.Unit ??= pi.Unit; line.OtherWeek = pi.Week != week;
        }
        else if (injectionId is int i && names.Injections.TryGetValue(i, out var inj))
        {
            line.Name = inj.Name; line.Kind = "Injection"; line.Unit ??= inj.Unit; line.OutsidePackage = true;
        }
        else if (vitaminId is int v && names.Vitamins.TryGetValue(v, out var vit))
        {
            line.Name = vit.Name; line.Kind = "Vitamin"; line.Unit ??= vit.Unit; line.OutsidePackage = true;
        }
        else
        {
            line.Name = customName ?? "—"; line.Kind = "Other"; line.OutsidePackage = true;
        }
        return line;
    }

    /// <summary>
    /// M4d: what the follow-up form shows. This week's lines start from the patient's plan once one exists,
    /// otherwise from the package, and planned lines start ticked; editing shows what was given. The coming
    /// weeks start from the plan, or the package. After a refused save, the posted lines are shown again.
    /// </summary>
    public async Task<ServiceResult<FollowUpFormDto>> GetFollowUpFormAsync(int enrollmentId, int? followUpId, RecordFollowUpDto? posted = null)
    {
        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment is null) return ServiceResult<FollowUpFormDto>.Failure("Enrollment not found.");

        var names = await LoadItemNamesAsync(enrollment.PackageId);
        var plan = await _uow.EnrollmentPlannedItems.Query().Where(p => p.EnrollmentId == enrollmentId).ToListAsync();
        var hasPlan = plan.Count > 0;
        var latest = await _uow.WeeklyFollowUps.GetMaxWeekNumberAsync(enrollmentId) ?? 0;

        WeeklyFollowUp? followUp = null;
        if (followUpId is int id)
        {
            followUp = await _uow.WeeklyFollowUps.GetByIdAsync(id);
            if (followUp is null || followUp.EnrollmentId != enrollmentId)
                return ServiceResult<FollowUpFormDto>.Failure("Follow-up not found.");
        }
        var week = followUp?.WeekNumber ?? latest + 1;

        var form = new FollowUpFormDto
        {
            EnrollmentId = enrollmentId,
            Week = week,
            PackageItems = names.Package.Select(p => Line(names, p.Value.Week, p.Key, null, null, null, p.Value.Quantity, null, p.Value.Notes, false)).ToList(),
            Injections = names.Injections.Select(i => new CatalogueChoiceDto(i.Key, i.Value.Name, i.Value.Unit)).OrderBy(c => c.Name).ToList(),
            Vitamins = names.Vitamins.Select(v => new CatalogueChoiceDto(v.Key, v.Value.Name, v.Value.Unit)).OrderBy(c => c.Name).ToList(),
        };
        for (var w = Math.Max(latest, week) + 1; w <= FollowUpWeeks; w++) form.PlanWeeks.Add(w);

        if (posted is not null)
        {
            form.Lines = posted.AdministeredItems.Select(a => Line(names, week, a.PackageItemId, a.InjectionId, a.VitaminId,
                a.CustomName, a.ActualQuantity ?? 0m, a.Unit, a.Notes, a.Given)).ToList();
            form.Plan = posted.PlannedItems.Select(p => Line(names, p.WeekNumber, p.PackageItemId, p.InjectionId, p.VitaminId,
                p.CustomName, p.Quantity ?? 0m, p.Unit, p.Notes, false)).ToList();
            return ServiceResult<FollowUpFormDto>.Success(form);
        }

        IEnumerable<FollowUpLineDto> PackageWeek(int w, bool given) => names.Package.Where(p => p.Value.Week == w)
            .OrderBy(p => p.Key)
            .Select(p => Line(names, w, p.Key, null, null, null, p.Value.Quantity, null, p.Value.Notes, given));
        IEnumerable<FollowUpLineDto> PlanWeek(int w, bool given) => plan.Where(p => p.WeekNumber == w).OrderBy(p => p.Id)
            .Select(p => Line(names, w, p.PackageItemId, p.InjectionId, p.VitaminId, p.CustomName, p.Quantity, p.Unit, p.Notes, given));

        if (followUp is not null)
        {
            var items = await _uow.WeeklyAdministeredItems.FindAsync(a => a.FollowUpId == followUp.Id);
            form.Lines.AddRange(items.OrderBy(a => a.Id).Select(a => Line(names, week, a.PackageItemId, a.InjectionId, a.VitaminId,
                a.CustomName, a.ActualQuantity, a.Unit, a.Notes, true)));
        }
        else
        {
            // Planned items start ticked (decision 2): the plan is what's expected this week.
            form.Lines.AddRange(hasPlan ? PlanWeek(week, true) : PackageWeek(week, true));
        }
        // The package's other items, unticked, for something given early or late.
        var shown = form.Lines.Where(l => l.PackageItemId.HasValue).Select(l => l.PackageItemId!.Value).ToHashSet();
        form.Lines.AddRange(names.Package.Where(p => !shown.Contains(p.Key)).OrderBy(p => p.Value.Week).ThenBy(p => p.Key)
            .Select(p => Line(names, week, p.Key, null, null, null, p.Value.Quantity, null, p.Value.Notes, false)));

        foreach (var w in form.PlanWeeks)
            form.Plan.AddRange(hasPlan ? PlanWeek(w, false) : PackageWeek(w, false));

        return ServiceResult<FollowUpFormDto>.Success(form);
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
    public async Task<ServiceResult<WeeklyFollowUpDto>> UpdateFollowUpAsync(
        int followUpId, RecordFollowUpDto request, int updatedBy)
    {
        var followUp = await _uow.WeeklyFollowUps.GetByIdAsync(followUpId);
        if (followUp is null)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Follow-up not found.");
        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(followUp.EnrollmentId);
        if (enrollment is null)
            return ServiceResult<WeeklyFollowUpDto>.Failure("Enrollment not found.");
        if (enrollment.Status == EnrollmentStatus.Cancelled)
            return ServiceResult<WeeklyFollowUpDto>.Failure(CancelledFollowUpMessage);

        var firstPlanWeek = Math.Max(await _uow.WeeklyFollowUps.GetMaxWeekNumberAsync(followUp.EnrollmentId) ?? 0, followUp.WeekNumber) + 1;
        var checkedInput = await CheckFollowUpInputAsync(enrollment, request, firstPlanWeek);
        if (checkedInput.Error is not null)
            return ServiceResult<WeeklyFollowUpDto>.Failure(checkedInput.Error);

        // M4b: the same week is changed (its number never changes), including its items and lab results,
        // which are replaced by what the form now lists.
        ApplyFollowUpFields(followUp, request, checkedInput.Diet);
        _uow.WeeklyFollowUps.Update(followUp);
        _uow.WeeklyAdministeredItems.DeleteRange(await _uow.WeeklyAdministeredItems.FindAsync(a => a.FollowUpId == followUpId));
        _uow.WeeklyFollowUpLabResults.DeleteRange(await _uow.WeeklyFollowUpLabResults.FindAsync(l => l.FollowUpId == followUpId));
        await AddFollowUpItemsAsync(followUpId, request, updatedBy);
        await SavePlanAsync(followUp.EnrollmentId, request, firstPlanWeek, updatedBy);   // M4d
        await _audit.RecordAsync(AuditActions.FollowUpEdited, "WeeklyFollowUp", followUpId,
            new { enrollmentId = followUp.EnrollmentId, week = followUp.WeekNumber });
        await _uow.SaveChangesAsync();

        return ServiceResult<WeeklyFollowUpDto>.Success(
            await BuildFollowUpDtoAsync(followUp));
    }

    public const string CancelledFollowUpMessage = "This enrollment is cancelled; its follow-ups can't be changed.";
    public const string OnlyLatestWeekMessage = "Only the latest week's follow-up can be deleted.";

    public async Task<ServiceResult> DeleteFollowUpAsync(int followUpId)
    {
        var followUp = await _uow.WeeklyFollowUps.GetByIdAsync(followUpId);
        if (followUp is null)
            return ServiceResult.Failure("Follow-up not found.");
        var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(followUp.EnrollmentId);
        if (enrollment is null)
            return ServiceResult.Failure("Enrollment not found.");
        if (enrollment.Status == EnrollmentStatus.Cancelled)
            return ServiceResult.Failure(CancelledFollowUpMessage);

        // M4b (decision 3): only the latest week, so weeks stay 1, 2, 3, 4 with no gaps.
        var latest = await _uow.WeeklyFollowUps.GetMaxWeekNumberAsync(followUp.EnrollmentId);
        if (followUp.WeekNumber != latest)
            return ServiceResult.Failure(OnlyLatestWeekMessage);

        // Deleting the last week of a completed enrollment makes it active again, unless the patient has
        // since started another active enrollment (one active enrollment per patient).
        var reopen = enrollment.Status == EnrollmentStatus.Completed;
        if (reopen && await _uow.NutritionEnrollments.Query()
                .AnyAsync(e => e.PatientId == enrollment.PatientId && e.Id != enrollment.Id && e.Status == EnrollmentStatus.Active))
            return ServiceResult.Failure(
                "The patient has another active enrollment, so this completed one can't be reopened by deleting its last week.");

        _uow.WeeklyAdministeredItems.DeleteRange(await _uow.WeeklyAdministeredItems.FindAsync(a => a.FollowUpId == followUpId));
        _uow.WeeklyFollowUpLabResults.DeleteRange(await _uow.WeeklyFollowUpLabResults.FindAsync(l => l.FollowUpId == followUpId));
        _uow.WeeklyFollowUps.Delete(followUp);

        if (reopen)
        {
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.UpdatedAt = DateTime.UtcNow;
            _uow.NutritionEnrollments.Update(enrollment);
            await _audit.RecordAsync(AuditActions.EnrollmentStatusChanged, "NutritionEnrollment", enrollment.Id,
                new { from = nameof(EnrollmentStatus.Completed), to = nameof(EnrollmentStatus.Active), reason = "last week deleted" });
        }
        await _audit.RecordAsync(AuditActions.FollowUpDeleted, "WeeklyFollowUp", followUpId,
            new { enrollmentId = followUp.EnrollmentId, week = followUp.WeekNumber });
        await _uow.SaveChangesAsync();

        return ServiceResult.Success(reopen
            ? "Follow-up deleted. The enrollment is active again."
            : "Follow-up deleted.");
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

        // M4d: names of items given from the catalogues, outside the package.
        var extraInjectionIds = administeredItems.Where(a => a.InjectionId.HasValue).Select(a => a.InjectionId!.Value).Distinct().ToList();
        var extraVitaminIds = administeredItems.Where(a => a.VitaminId.HasValue).Select(a => a.VitaminId!.Value).Distinct().ToList();
        var injectionNames = extraInjectionIds.Count == 0 ? new Dictionary<int, string>()
            : await _uow.InjectionTypes.Query().Where(i => extraInjectionIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.InjectionName);
        var vitaminNames = extraVitaminIds.Count == 0 ? new Dictionary<int, string>()
            : await _uow.VitaminTypes.Query().Where(v => extraVitaminIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.VitaminName);

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
            else if (a.InjectionId is int injectionId)
            {
                itemName = injectionNames.GetValueOrDefault(injectionId, "");
            }
            else if (a.VitaminId is int vitaminId)
            {
                itemName = vitaminNames.GetValueOrDefault(vitaminId, "");
            }
            else if (a.CustomName is not null)
            {
                itemName = a.CustomName;
            }

            administeredDtos.Add(new AdministeredItemDto
            {
                Id = a.Id,
                PackageItemId = a.PackageItemId,
                InjectionId = a.InjectionId,
                VitaminId = a.VitaminId,
                CustomName = a.CustomName,
                Unit = a.Unit ?? packageItem?.Unit,
                OutsidePackage = a.PackageItemId is null,
                Notes = a.Notes,
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
                AnalysisTypeId = l.AnalysisTypeId,
                Notes = l.Notes,
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
            HeightCm = f.HeightCm,   // M4b: was missing, so editing a follow-up cleared the height
            BMI = f.BMI,
            BodyFatPercent = f.BodyFatPercent,
            MuscleMassKg = f.MuscleMassKg,
            WaistCircumferenceCm = f.WaistCircumferenceCm,
            BloodPressure = (f.BloodPressureSys.HasValue && f.BloodPressureDia.HasValue)
                ? $"{f.BloodPressureSys}/{f.BloodPressureDia}"
                : null,
            BloodPressureSys = f.BloodPressureSys,
            BloodPressureDia = f.BloodPressureDia,
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