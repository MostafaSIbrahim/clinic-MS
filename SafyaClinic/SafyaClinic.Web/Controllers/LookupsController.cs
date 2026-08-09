using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class LookupsController : BaseController
    {
        private readonly SafyaDbContext _context;

        public LookupsController(SafyaDbContext context)
        {
            _context = context;
        }

        // --- Index Dashboard for Lookups ---
        public IActionResult Index()
        {
            return View();
        }

        // --- Analysis Types ---
        public async Task<IActionResult> AnalysisTypes()
        {
            var types = await _context.AnalysisTypes.ToListAsync();
            return View(types);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnalysisType(AnalysisType model)
        {
            if (ModelState.IsValid)
            {
                _context.AnalysisTypes.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Analysis Type added successfully.";
                return RedirectToAction(nameof(AnalysisTypes));
            }
            return View(nameof(AnalysisTypes), await _context.AnalysisTypes.ToListAsync());
        }

        // --- Treatment Types ---
        public async Task<IActionResult> TreatmentTypes()
        {
            var types = await _context.TreatmentTypes.ToListAsync();
            return View(types);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTreatmentType(TreatmentType model)
        {
            if (ModelState.IsValid)
            {
                _context.TreatmentTypes.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Treatment Type added successfully.";
                return RedirectToAction(nameof(TreatmentTypes));
            }
            return View(nameof(TreatmentTypes), await _context.TreatmentTypes.ToListAsync());
        }

        // --- Injection Types ---
        public async Task<IActionResult> InjectionTypes()
        {
            var types = await _context.InjectionTypes.ToListAsync();
            return View(types);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInjectionType(InjectionType model)
        {
            if (ModelState.IsValid)
            {
                _context.InjectionTypes.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Injection Type added successfully.";
                return RedirectToAction(nameof(InjectionTypes));
            }
            return View(nameof(InjectionTypes), await _context.InjectionTypes.ToListAsync());
        }

        // --- Vitamin Types ---
        public async Task<IActionResult> VitaminTypes()
        {
            var types = await _context.VitaminTypes.ToListAsync();
            return View(types);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVitaminType(VitaminType model)
        {
            if (ModelState.IsValid)
            {
                _context.VitaminTypes.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Vitamin Type added successfully.";
                return RedirectToAction(nameof(VitaminTypes));
            }
            return View(nameof(VitaminTypes), await _context.VitaminTypes.ToListAsync());
        }
    }
}