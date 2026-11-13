using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Catalogue;

/// <summary>M5a: the four catalogues edited on one screen.</summary>
public enum CatalogueKind { Treatment, Analysis, Injection, Vitamin }

/// <summary>One catalogue item for the list and the edit form. Fields that a kind doesn't use stay empty.</summary>
public sealed class CatalogueItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal? Cost { get; init; }               // treatment, analysis
    public int? DurationMinutes { get; init; }        // treatment
    public string? Category { get; init; }            // treatment: InternalMedicine or Nutritional
    public string? Preparation { get; init; }         // analysis
    public string? Unit { get; init; }                // injection, vitamin
    public string? DefaultDosage { get; init; }       // injection
    public string? Formulation { get; init; }         // vitamin
    public bool IsActive { get; init; }
    public bool InUse { get; init; }
}

/// <summary>
/// M5a: what the edit form sends. Its own input model: the form can't set the record's id, dates or anything it
/// doesn't show (F16: the old screens saved the form straight into database objects).
/// </summary>
public sealed class CatalogueItemForm
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Cost { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Category { get; set; }
    public string? Preparation { get; set; }
    public string? Unit { get; set; }
    public string? DefaultDosage { get; set; }
    public string? Formulation { get; set; }
    public bool IsActive { get; set; } = true;
}

public interface ICatalogueService
{
    Task<IReadOnlyList<CatalogueItemDto>> ListAsync(CatalogueKind kind, string? search = null);
    Task<CatalogueItemDto?> GetAsync(CatalogueKind kind, int id);
    /// <summary>Adds (Id 0) or updates an item. Returns its id, or the problems with the form.</summary>
    Task<ServiceResult<int>> SaveAsync(CatalogueKind kind, CatalogueItemForm form);
    /// <summary>Deletes an item never used; one already used is made inactive instead, so history keeps its name.</summary>
    Task<ServiceResult> DeleteAsync(CatalogueKind kind, int id);
}

public sealed class CatalogueService : ICatalogueService
{
    public const string Deleted = "Deleted.";
    public const string Deactivated = "It's already used, so it was made inactive instead of deleted. You can make it active again.";

    private readonly IUnitOfWork _uow;

    public CatalogueService(IUnitOfWork uow) => _uow = uow;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ── Reading ────────────────────────────────────────────

    public async Task<IReadOnlyList<CatalogueItemDto>> ListAsync(CatalogueKind kind, string? search = null)
    {
        var term = Clean(search)?.ToLower();
        var items = kind switch
        {
            CatalogueKind.Treatment => (await _uow.TreatmentTypes.Query()
                .Where(t => term == null || t.TypeName.ToLower().Contains(term))
                .OrderBy(t => t.Category).ThenBy(t => t.TypeName).ToListAsync()).Select(Map).ToList(),
            CatalogueKind.Analysis => (await _uow.AnalysisTypes.Query()
                .Where(a => term == null || a.TypeName.ToLower().Contains(term))
                .OrderBy(a => a.TypeName).ToListAsync()).Select(Map).ToList(),
            CatalogueKind.Injection => (await _uow.InjectionTypes.Query()
                .Where(i => term == null || i.InjectionName.ToLower().Contains(term))
                .OrderBy(i => i.InjectionName).ToListAsync()).Select(Map).ToList(),
            _ => (await _uow.VitaminTypes.Query()
                .Where(v => term == null || v.VitaminName.ToLower().Contains(term))
                .OrderBy(v => v.VitaminName).ToListAsync()).Select(Map).ToList(),
        };
        var used = await UsedIdsAsync(kind, items.Select(i => i.Id).ToList());
        return items.Select(i => With(i, used.Contains(i.Id))).OrderByDescending(i => i.IsActive).ToList();
    }

    public async Task<CatalogueItemDto?> GetAsync(CatalogueKind kind, int id)
    {
        CatalogueItemDto? item = kind switch
        {
            CatalogueKind.Treatment => await _uow.TreatmentTypes.Query().Where(t => t.Id == id).Select(t => Map(t)).FirstOrDefaultAsync(),
            CatalogueKind.Analysis => await _uow.AnalysisTypes.Query().Where(a => a.Id == id).Select(a => Map(a)).FirstOrDefaultAsync(),
            CatalogueKind.Injection => await _uow.InjectionTypes.Query().Where(i => i.Id == id).Select(i => Map(i)).FirstOrDefaultAsync(),
            _ => await _uow.VitaminTypes.Query().Where(v => v.Id == id).Select(v => Map(v)).FirstOrDefaultAsync(),
        };
        if (item is null) return null;
        return With(item, (await UsedIdsAsync(kind, new List<int> { id })).Contains(id));
    }

    private static CatalogueItemDto With(CatalogueItemDto i, bool inUse) => new()
    {
        Id = i.Id, Name = i.Name, Description = i.Description, Cost = i.Cost, DurationMinutes = i.DurationMinutes,
        Category = i.Category, Preparation = i.Preparation, Unit = i.Unit, DefaultDosage = i.DefaultDosage,
        Formulation = i.Formulation, IsActive = i.IsActive, InUse = inUse,
    };

    private static CatalogueItemDto Map(TreatmentType t) => new()
    {
        Id = t.Id, Name = t.TypeName, Description = t.Description, Cost = t.DefaultCost, DurationMinutes = t.DurationMinutes,
        Category = t.Category.ToString(), IsActive = t.IsActive,
    };
    private static CatalogueItemDto Map(AnalysisType a) => new()
    {
        Id = a.Id, Name = a.TypeName, Description = a.Description, Cost = a.DefaultCost, Preparation = a.PreparationInstructions,
        IsActive = a.IsActive,
    };
    private static CatalogueItemDto Map(InjectionType i) => new()
    {
        Id = i.Id, Name = i.InjectionName, Description = i.Description, Unit = i.Unit, DefaultDosage = i.DefaultDosage,
        IsActive = i.IsActive,
    };
    private static CatalogueItemDto Map(VitaminType v) => new()
    {
        Id = v.Id, Name = v.VitaminName, Description = v.Description, Unit = v.Unit, Formulation = v.Formulation,
        IsActive = v.IsActive,
    };

    /// <summary>The items of <paramref name="ids"/> that anything refers to: such an item is never deleted.</summary>
    private async Task<HashSet<int>> UsedIdsAsync(CatalogueKind kind, List<int> ids)
    {
        if (ids.Count == 0) return new HashSet<int>();
        IEnumerable<int> used = kind switch
        {
            CatalogueKind.Treatment => await _uow.Reservations.Query()
                .Where(r => ids.Contains(r.TreatmentTypeId)).Select(r => r.TreatmentTypeId).Distinct().ToListAsync(),
            CatalogueKind.Analysis => (await _uow.MedicalAnalyses.Query()
                    .Where(m => ids.Contains(m.AnalysisTypeId)).Select(m => m.AnalysisTypeId).Distinct().ToListAsync())
                .Concat(await _uow.WeeklyFollowUpLabResults.Query()
                    .Where(l => ids.Contains(l.AnalysisTypeId)).Select(l => l.AnalysisTypeId).Distinct().ToListAsync()),
            CatalogueKind.Injection => (await _uow.PackageItems.Query()
                    .Where(p => p.InjectionId != null && ids.Contains(p.InjectionId.Value)).Select(p => p.InjectionId!.Value).Distinct().ToListAsync())
                .Concat(await _uow.WeeklyAdministeredItems.Query()
                    .Where(a => a.InjectionId != null && ids.Contains(a.InjectionId.Value)).Select(a => a.InjectionId!.Value).Distinct().ToListAsync())
                .Concat(await _uow.EnrollmentPlannedItems.Query()
                    .Where(p => p.InjectionId != null && ids.Contains(p.InjectionId.Value)).Select(p => p.InjectionId!.Value).Distinct().ToListAsync()),
            _ => (await _uow.PackageItems.Query()
                    .Where(p => p.VitaminId != null && ids.Contains(p.VitaminId.Value)).Select(p => p.VitaminId!.Value).Distinct().ToListAsync())
                .Concat(await _uow.WeeklyAdministeredItems.Query()
                    .Where(a => a.VitaminId != null && ids.Contains(a.VitaminId.Value)).Select(a => a.VitaminId!.Value).Distinct().ToListAsync())
                .Concat(await _uow.EnrollmentPlannedItems.Query()
                    .Where(p => p.VitaminId != null && ids.Contains(p.VitaminId.Value)).Select(p => p.VitaminId!.Value).Distinct().ToListAsync()),
        };
        return used.ToHashSet();
    }

    // ── Saving ─────────────────────────────────────────────

    /// <summary>The checks for one kind. Returns the problems, field by field.</summary>
    public static Dictionary<string, string> Check(CatalogueKind kind, CatalogueItemForm f)
    {
        var problems = new Dictionary<string, string>();
        var name = Clean(f.Name);
        if (name is null) problems[nameof(f.Name)] = "Enter the name.";
        else if (name.Length > 100) problems[nameof(f.Name)] = "The name can be at most 100 characters.";
        if (Clean(f.Description) is { Length: > 500 }) problems[nameof(f.Description)] = "The description can be at most 500 characters.";

        switch (kind)
        {
            case CatalogueKind.Treatment:
                var category = Enum.TryParse<TreatmentCategory>(f.Category, out var c) && Enum.IsDefined(c) ? c : (TreatmentCategory?)null;
                if (category is null) problems[nameof(f.Category)] = "Choose the category.";
                if (f.Cost is null) problems[nameof(f.Cost)] = "Enter the price.";
                else if (f.Cost < 0) problems[nameof(f.Cost)] = "The price can't be negative.";
                // Only the nutrition "Follow-up" visit may be free: it is paid for by the enrollment (unchanged rule).
                else if (f.Cost == 0 && !(category == TreatmentCategory.Nutritional
                         && string.Equals(name, "Follow-up", StringComparison.OrdinalIgnoreCase)))
                    problems[nameof(f.Cost)] = "The price must be above zero. Only the nutrition \"Follow-up\" visit may be free.";
                if (f.DurationMinutes is null or < 5 or > 480)
                    problems[nameof(f.DurationMinutes)] = "Enter a duration between 5 and 480 minutes.";
                break;
            case CatalogueKind.Analysis:
                if (f.Cost < 0) problems[nameof(f.Cost)] = "The price can't be negative.";
                if (Clean(f.Preparation) is { Length: > 1000 }) problems[nameof(f.Preparation)] = "The preparation can be at most 1000 characters.";
                break;
            case CatalogueKind.Injection:
            case CatalogueKind.Vitamin:
                var unit = Clean(f.Unit);
                if (unit is null) problems[nameof(f.Unit)] = "Enter the unit (for example ml, mg or tablet).";
                else if (unit.Length > 50) problems[nameof(f.Unit)] = "The unit can be at most 50 characters.";
                if (Clean(kind == CatalogueKind.Injection ? f.DefaultDosage : f.Formulation) is { Length: > 100 })
                    problems[kind == CatalogueKind.Injection ? nameof(f.DefaultDosage) : nameof(f.Formulation)] = "This can be at most 100 characters.";
                break;
        }
        return problems;
    }

    public async Task<ServiceResult<int>> SaveAsync(CatalogueKind kind, CatalogueItemForm f)
    {
        var problems = Check(kind, f);
        if (problems.Count > 0) return ServiceResult<int>.Failure(problems.Values.ToList());

        var name = Clean(f.Name)!;
        var lower = name.ToLower();
        var treatmentCategory = kind == CatalogueKind.Treatment ? Enum.Parse<TreatmentCategory>(f.Category!) : default;
        var duplicate = kind switch
        {
            CatalogueKind.Treatment => await _uow.TreatmentTypes.Query().AnyAsync(t => t.Id != f.Id && t.TypeName.ToLower() == lower
                && t.Category == treatmentCategory),
            CatalogueKind.Analysis => await _uow.AnalysisTypes.Query().AnyAsync(a => a.Id != f.Id && a.TypeName.ToLower() == lower),
            CatalogueKind.Injection => await _uow.InjectionTypes.Query().AnyAsync(i => i.Id != f.Id && i.InjectionName.ToLower() == lower),
            _ => await _uow.VitaminTypes.Query().AnyAsync(v => v.Id != f.Id && v.VitaminName.ToLower() == lower),
        };
        if (duplicate) return ServiceResult<int>.Failure("An item with this name already exists.");

        switch (kind)
        {
            case CatalogueKind.Treatment:
            {
                var t = f.Id == 0 ? new TreatmentType() : await _uow.TreatmentTypes.GetByIdAsync(f.Id);
                if (t is null) return ServiceResult<int>.Failure("Item not found.");
                t.TypeName = name; t.Description = Clean(f.Description); t.DefaultCost = f.Cost;
                t.DurationMinutes = f.DurationMinutes!.Value; t.Category = treatmentCategory; t.IsActive = f.IsActive;
                if (f.Id == 0) await _uow.TreatmentTypes.AddAsync(t); else _uow.TreatmentTypes.Update(t);
                await _uow.SaveChangesAsync();
                return ServiceResult<int>.Success(t.Id);
            }
            case CatalogueKind.Analysis:
            {
                var a = f.Id == 0 ? new AnalysisType() : await _uow.AnalysisTypes.GetByIdAsync(f.Id);
                if (a is null) return ServiceResult<int>.Failure("Item not found.");
                a.TypeName = name; a.Description = Clean(f.Description); a.DefaultCost = f.Cost;
                a.PreparationInstructions = Clean(f.Preparation); a.IsActive = f.IsActive;
                if (f.Id == 0) await _uow.AnalysisTypes.AddAsync(a); else _uow.AnalysisTypes.Update(a);
                await _uow.SaveChangesAsync();
                return ServiceResult<int>.Success(a.Id);
            }
            case CatalogueKind.Injection:
            {
                var i = f.Id == 0 ? new InjectionType() : await _uow.InjectionTypes.GetByIdAsync(f.Id);
                if (i is null) return ServiceResult<int>.Failure("Item not found.");
                i.InjectionName = name; i.Description = Clean(f.Description); i.Unit = Clean(f.Unit)!;
                i.DefaultDosage = Clean(f.DefaultDosage); i.IsActive = f.IsActive;
                if (f.Id == 0) await _uow.InjectionTypes.AddAsync(i); else _uow.InjectionTypes.Update(i);
                await _uow.SaveChangesAsync();
                return ServiceResult<int>.Success(i.Id);
            }
            default:
            {
                var v = f.Id == 0 ? new VitaminType() : await _uow.VitaminTypes.GetByIdAsync(f.Id);
                if (v is null) return ServiceResult<int>.Failure("Item not found.");
                v.VitaminName = name; v.Description = Clean(f.Description); v.Unit = Clean(f.Unit)!;
                v.Formulation = Clean(f.Formulation); v.IsActive = f.IsActive;
                if (f.Id == 0) await _uow.VitaminTypes.AddAsync(v); else _uow.VitaminTypes.Update(v);
                await _uow.SaveChangesAsync();
                return ServiceResult<int>.Success(v.Id);
            }
        }
    }

    public async Task<ServiceResult> DeleteAsync(CatalogueKind kind, int id)
    {
        var inUse = (await UsedIdsAsync(kind, new List<int> { id })).Contains(id);
        switch (kind)
        {
            case CatalogueKind.Treatment:
            {
                var t = await _uow.TreatmentTypes.GetByIdAsync(id);
                if (t is null) return ServiceResult.Failure("Item not found.");
                if (inUse) { t.IsActive = false; _uow.TreatmentTypes.Update(t); } else _uow.TreatmentTypes.Delete(t);
                break;
            }
            case CatalogueKind.Analysis:
            {
                var a = await _uow.AnalysisTypes.GetByIdAsync(id);
                if (a is null) return ServiceResult.Failure("Item not found.");
                if (inUse) { a.IsActive = false; _uow.AnalysisTypes.Update(a); } else _uow.AnalysisTypes.Delete(a);
                break;
            }
            case CatalogueKind.Injection:
            {
                var i = await _uow.InjectionTypes.GetByIdAsync(id);
                if (i is null) return ServiceResult.Failure("Item not found.");
                if (inUse) { i.IsActive = false; _uow.InjectionTypes.Update(i); } else _uow.InjectionTypes.Delete(i);
                break;
            }
            default:
            {
                var v = await _uow.VitaminTypes.GetByIdAsync(id);
                if (v is null) return ServiceResult.Failure("Item not found.");
                if (inUse) { v.IsActive = false; _uow.VitaminTypes.Update(v); } else _uow.VitaminTypes.Delete(v);
                break;
            }
        }
        await _uow.SaveChangesAsync();
        return ServiceResult.Success(inUse ? Deactivated : Deleted);
    }
}
