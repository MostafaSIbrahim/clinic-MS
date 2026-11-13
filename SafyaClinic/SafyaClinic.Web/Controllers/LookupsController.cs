using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Catalogue;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers
{
    /// <summary>
    /// Settings: the lookups home and, since M5a, one editor for the four catalogues (treatment, analysis,
    /// injection and vitamin types). Forms use their own input model and the rules live in ICatalogueService;
    /// the old per-catalogue screens (and the duplicate ones under Nutrition) redirect here.
    /// </summary>
    [RequirePermission(Permissions.Settings.Lookups)]
    public class LookupsController : BaseController
    {
        private readonly ICatalogueService _catalogue;

        public LookupsController(ICatalogueService catalogue) => _catalogue = catalogue;

        public IActionResult Index() => View();

        private static CatalogueKind KindOf(string? kind) =>
            Enum.TryParse<CatalogueKind>(kind, ignoreCase: true, out var k) && Enum.IsDefined(k) ? k : CatalogueKind.Treatment;

        public async Task<IActionResult> Catalogue(string? kind, string? search)
        {
            var k = KindOf(kind);
            ViewBag.Kind = k;
            ViewBag.Search = search;
            return View(await _catalogue.ListAsync(k, search));
        }

        [HttpGet]
        public async Task<IActionResult> EditCatalogueItem(string? kind, int id = 0)
        {
            var k = KindOf(kind);
            var form = new CatalogueItemForm { Category = k == CatalogueKind.Treatment ? "InternalMedicine" : null, DurationMinutes = k == CatalogueKind.Treatment ? 30 : null };
            if (id != 0)
            {
                var item = await _catalogue.GetAsync(k, id);
                if (item is null) { Error("Item not found."); return RedirectToAction(nameof(Catalogue), new { kind = k }); }
                form = new CatalogueItemForm
                {
                    Id = item.Id, Name = item.Name, Description = item.Description, Cost = item.Cost, DurationMinutes = item.DurationMinutes,
                    Category = item.Category, Preparation = item.Preparation, Unit = item.Unit, DefaultDosage = item.DefaultDosage,
                    Formulation = item.Formulation, IsActive = item.IsActive,
                };
                ViewBag.InUse = item.InUse;
            }
            ViewBag.Kind = k;
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCatalogueItem(string? kind, CatalogueItemForm form)
        {
            var k = KindOf(kind);
            foreach (var (field, message) in CatalogueService.Check(k, form))
                ModelState.AddModelError(field, message);
            if (ModelState.IsValid)
            {
                var result = await _catalogue.SaveAsync(k, form);
                if (result.IsSuccess)
                    return RedirectWithSuccess(form.Id == 0 ? "Added." : "Saved.", nameof(Catalogue), routeValues: new { kind = k });
                ApplyErrors(result);
            }
            ViewBag.Kind = k;
            if (form.Id != 0) ViewBag.InUse = (await _catalogue.GetAsync(k, form.Id))?.InUse == true;
            return View(nameof(EditCatalogueItem), form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCatalogueItem(string? kind, int id)
        {
            var k = KindOf(kind);
            var result = await _catalogue.DeleteAsync(k, id);
            if (result.IsSuccess) Success(result.Message);
            else Error(result.Errors.FirstOrDefault() ?? "The item could not be deleted.");
            return RedirectToAction(nameof(Catalogue), new { kind = k });
        }

        // The old screens' addresses open the catalogue (M5a).
        public IActionResult TreatmentTypes() => RedirectToAction(nameof(Catalogue), new { kind = CatalogueKind.Treatment });
        public IActionResult AnalysisTypes() => RedirectToAction(nameof(Catalogue), new { kind = CatalogueKind.Analysis });
        public IActionResult InjectionTypes() => RedirectToAction(nameof(Catalogue), new { kind = CatalogueKind.Injection });
        public IActionResult VitaminTypes() => RedirectToAction(nameof(Catalogue), new { kind = CatalogueKind.Vitamin });
    }
}
