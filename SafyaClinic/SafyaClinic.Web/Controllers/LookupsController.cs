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
        /* public async Task<IActionResult> TreatmentTypes()
         {
             var types = await _context.TreatmentTypes.ToListAsync();
             return View(types);
         }*/
        public async Task<IActionResult> TreatmentTypes()
        {
            var treatmentTypes = await _context.TreatmentTypes
                .OrderBy(t => t.Category)
                .ThenBy(t => t.TypeName)
                .ToListAsync();

            return View(treatmentTypes);
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
        // POST: Lookups/SaveTreatmentType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTreatmentType(TreatmentType model)
        {
            if (string.IsNullOrWhiteSpace(model.TypeName))
            {
                ModelState.AddModelError(nameof(model.TypeName), "Treatment name is required.");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields properly (Treatment Name and Default Cost are required).";
                return RedirectToAction(nameof(TreatmentTypes));
            }

            try
            {
                if (model.Id == 0)
                {
                    // Create New Treatment Type
                    model.TypeName = model.TypeName.Trim();
                    model.Description = model.Description?.Trim();
                    _context.TreatmentTypes.Add(model);
                    TempData["Success"] = "Treatment type created successfully.";
                }
                else
                {
                    // Update Existing Treatment Type
                    var existing = await _context.TreatmentTypes.FindAsync(model.Id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Treatment type not found.";
                        return RedirectToAction(nameof(TreatmentTypes));
                    }

                    existing.TypeName = model.TypeName.Trim();
                    existing.Category = model.Category;
                    existing.DefaultCost = model.DefaultCost;
                    existing.Description = model.Description?.Trim();
                    existing.IsActive = model.IsActive;

                    _context.TreatmentTypes.Update(existing);
                    TempData["Success"] = "Treatment type updated successfully.";
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while saving: {ex.Message}";
            }

            return RedirectToAction(nameof(TreatmentTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTreatmentType(int id)
        {
            try
            {
                var treatmentType = await _context.TreatmentTypes.FindAsync(id);
                if (treatmentType == null)
                {
                    TempData["Error"] = "Treatment type not found.";
                    return RedirectToAction(nameof(TreatmentTypes));
                }

                bool isUsed = await _context.Treatments.AnyAsync(t => t.Id == id);
                if (isUsed)
                {
                    treatmentType.IsActive = false;
                    _context.TreatmentTypes.Update(treatmentType);
                    TempData["Warning"] = "Treatment type is linked to existing records, so it has been deactivated instead of deleted.";
                }
                else
                {
                    _context.TreatmentTypes.Remove(treatmentType);
                    TempData["Success"] = "Treatment type deleted successfully.";
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting treatment type: {ex.Message}";
            }

            return RedirectToAction(nameof(TreatmentTypes));
        }
        // POST: Lookups/SaveInjectionType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveInjectionType(InjectionType model)
        {
            if (string.IsNullOrWhiteSpace(model.InjectionName))
            {
                ModelState.AddModelError(nameof(model.InjectionName), "Injection name is required.");
            }
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields properly (Injection Name and Default Cost are required).";
                return RedirectToAction(nameof(InjectionTypes));
            }
            try
            {
                if (model.Id == 0)
                {
                    // Create New Injection Type
                    model.InjectionName = model.InjectionName.Trim();
                    model.Description = model.Description?.Trim();
                    _context.InjectionTypes.Add(model);
                    TempData["Success"] = "Injection type created successfully.";
                }
                else
                {
                    // Update Existing Injection Type
                    var existing = await _context.InjectionTypes.FindAsync(model.Id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Injection type not found.";
                        return RedirectToAction(nameof(InjectionTypes));
                    }
                    existing.InjectionName = model.InjectionName.Trim();
                    existing.DefaultDosage = model.DefaultDosage;
                    existing.Description = model.Description?.Trim();
                    existing.IsActive = model.IsActive;
                    _context.InjectionTypes.Update(existing);
                    TempData["Success"] = "Injection type updated successfully.";
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while saving: {ex.Message}";
            }
            return RedirectToAction(nameof(InjectionTypes));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteInjectionType(int id)
        {
            try
            {
                var injectionType = await _context.InjectionTypes.FindAsync(id);
                if (injectionType == null)
                {
                    TempData["Error"] = "Injection type not found.";
                    return RedirectToAction(nameof(InjectionTypes));
                }
                bool isUsed = await _context.TreatmentTypes.AnyAsync(i => i.Id == id);
                if (isUsed)
                {
                    injectionType.IsActive = false;
                    _context.InjectionTypes.Update(injectionType);
                    TempData["Warning"] = "Injection type is linked to existing records, so it has been deactivated instead of deleted.";
                }
                else
                {
                    _context.InjectionTypes.Remove(injectionType);
                    TempData["Success"] = "Injection type deleted successfully.";
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting injection type: {ex.Message}";
            }
            return RedirectToAction(nameof(InjectionTypes));
        }
        // POST: Lookups/SaveVitaminType
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveVitaminType(VitaminType model)
        {
            if (string.IsNullOrWhiteSpace(model.VitaminName))
            {
                ModelState.AddModelError(nameof(model.VitaminName), "Vitamin name is required.");
            }
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields properly.";
                return RedirectToAction(nameof(VitaminTypes));
            }
            try
            {
                if (model.Id == 0)
                {
                    // Create New Vitamin Type
                    model.VitaminName = model.VitaminName.Trim();
                    model.Description = model.Description?.Trim();
                    _context.VitaminTypes.Add(model);
                    TempData["Success"] = "Vitamin type created successfully.";
                }
                else
                {
                    // Update Existing Vitamin Type
                    var existing = await _context.VitaminTypes.FindAsync(model.Id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Vitamin type not found.";
                        return RedirectToAction(nameof(VitaminTypes));
                    }
                    existing.VitaminName = model.VitaminName.Trim();
                    existing.Description = model.Description?.Trim();
                    existing.IsActive = model.IsActive;
                    _context.VitaminTypes.Update(existing);
                    TempData["Success"] = "Vitamin type updated successfully.";
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while saving: {ex.Message}";
            }
            return RedirectToAction(nameof(VitaminTypes));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVitaminType(int id)
        {
            try
            {
                var vitaminType = await _context.VitaminTypes.FindAsync(id);
                if (vitaminType == null)
                {
                    TempData["Error"] = "Vitamin type not found.";
                    return RedirectToAction(nameof(VitaminTypes));
                }
                bool isUsed = await _context.Treatments.AnyAsync(v => v.Id == id);
                if (isUsed)
                {
                    vitaminType.IsActive = false;
                    _context.VitaminTypes.Update(vitaminType);
                    TempData["Warning"] = "Vitamin type is linked to existing records, so it has been deactivated instead of deleted.";
                }
                else
                {
                    _context.VitaminTypes.Remove(vitaminType);
                    TempData["Success"] = "Vitamin type deleted successfully.";
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting vitamin type: {ex.Message}";
            }
            return RedirectToAction(nameof(VitaminTypes));
        }
    }
}