using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Security;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Common;
namespace SafyaClinic.Web.Controllers
{
    [RequirePermission(Permissions.Nutrition.View)]
    public class NutritionController : BaseController
    {
        private readonly INutritionService _nutritionService;
        private readonly IPatientService _patientService;
        private readonly IUserService _userService;
        private readonly IClinicClock _clock;
        private readonly IAnalysisService _analysisService;

        public NutritionController(
            INutritionService nutritionService,
            IPatientService patientService,
            IUserService userService,
            IClinicClock clock,
            IAnalysisService analysisService)
        {
            _analysisService = analysisService;
            _nutritionService = nutritionService;
            _patientService = patientService;
            _userService = userService;
            _clock = clock;
        }

        // ── Packages ─────────────────────────────────────────────

        public async Task<IActionResult> Packages()
        {
            var result = await _nutritionService.GetActivePackagesAsync();
            return View(result.IsSuccess ? result.Data : Enumerable.Empty<NutritionPackageDto>());
        }

        public async Task<IActionResult> PackageDetails(int id)
        {
            var result = await _nutritionService.GetPackageByIdAsync(id);
            if (!result.IsSuccess) { Error("Package not found."); return RedirectToAction(nameof(Packages)); }
            return View(result.Data);
        }

        [HttpGet]
        [RequirePermission(Permissions.Nutrition.ManagePackages)]
        public async Task<IActionResult> CreatePackage()
        {
            await LoadCatalogViewBagsAsync();
            return View(new CreateNutritionPackageDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.ManagePackages)]
        public async Task<IActionResult> CreatePackage(CreateNutritionPackageDto model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCatalogViewBagsAsync();
                return View(model);
            }
            var result = await _nutritionService.CreatePackageAsync(model, CurrentUserId);
            if (!result.IsSuccess)
            {
                ApplyErrors(result);
                await LoadCatalogViewBagsAsync();
                return View(model);
            }
            return RedirectWithSuccess("Package created.", nameof(PackageDetails), routeValues: new { id = result.Data!.Id });
        }

        private async Task LoadCatalogViewBagsAsync()
        {
            var injections = await _nutritionService.GetInjectionTypesAsync();
            var vitamins = await _nutritionService.GetVitaminTypesAsync();
            ViewBag.InjectionTypes = injections.Data ?? Enumerable.Empty<InjectionTypeDto>();
            ViewBag.VitaminTypes = vitamins.Data ?? Enumerable.Empty<VitaminTypeDto>();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.ManagePackages)]
        public async Task<IActionResult> DeactivatePackage(int id)
        {
            // M4c: say what happened instead of always reporting success.
            var result = await _nutritionService.DeactivatePackageAsync(id);
            if (!result.IsSuccess) { Error(result.Errors.FirstOrDefault() ?? "The package could not be deactivated."); return RedirectToAction(nameof(PackageDetails), new { id }); }
            return RedirectWithSuccess("Package deactivated.", nameof(Packages));
        }

        // ── Enrollments ──────────────────────────────────────────

        // M1c: the enrollments list is the Nutrition tab of the patient workspace; old links land there.
        public IActionResult PatientEnrollments(int patientId) =>
            RedirectToAction("Details", "Patients", new { id = patientId, tab = SafyaClinic.Web.Models.PatientTabs.Nutrition });

        public async Task<IActionResult> EnrollmentDetails(int id)
        {
            var result = await _nutritionService.GetEnrollmentByIdAsync(id);
            if (!result.IsSuccess) { Error("Enrollment not found."); return RedirectToAction(nameof(Packages)); }
            return View(result.Data);
        }

        [HttpGet]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> Enroll(int patientId)
        {
            await LoadEnrollListsAsync();
            return View(new CreateEnrollmentDto
            {
                PatientId = patientId,
                StartDate = _clock.Today   // M4a: the clinic's date, not the server's
            });
        }

        // M4a: the package and doctor lists, also when a refused form is shown again (they came back empty before).
        private async Task LoadEnrollListsAsync()
        {
            var packages = await _nutritionService.GetActivePackagesAsync();
            var doctors = await _userService.GetDoctorsAsync();
            ViewBag.Packages = packages.Data;
            ViewBag.Doctors = doctors.Data;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> Enroll(CreateEnrollmentDto model)
        {
            if (!ModelState.IsValid)
            {
                await LoadEnrollListsAsync();
                return View(model);
            }
            var result = await _nutritionService.EnrollPatientAsync(model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); await LoadEnrollListsAsync(); return View(model); }
            return RedirectWithSuccess(
                "Patient enrolled.", nameof(EnrollmentDetails),
                routeValues: new { id = result.Data!.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> UpdateEnrollmentStatus(int id, string status, int patientId)
        {
            // M4a: the rules are the service's; say what happened instead of ignoring a refusal.
            var result = await _nutritionService.UpdateEnrollmentStatusAsync(id, status);
            if (result.IsSuccess) Success(string.IsNullOrEmpty(result.Message) ? "Enrollment status updated." : result.Message);
            else Error(result.Errors.FirstOrDefault() ?? "The enrollment status could not be changed.");
            return RedirectToAction(nameof(EnrollmentDetails), new { id });
        }

        // ── Weekly Follow-Ups ────────────────────────────────────
        // M4b: Edit really edits (it saved a new week before); items are chosen from the enrollment's package and
        // lab results from the analysis list (people had to type database numbers); a refused form keeps its lists.

        public async Task<IActionResult> FollowUpDetails(int id)
        {
            var result = await _nutritionService.GetFollowUpByIdAsync(id);
            if (!result.IsSuccess) { Error("Follow-up not found."); return RedirectToAction(nameof(Packages)); }
            var enrollment = await _nutritionService.GetEnrollmentByIdAsync(result.Data!.EnrollmentId);
            ViewBag.Enrollment = enrollment.Data;
            return View(result.Data);
        }

        /// <summary>
        /// The enrollment, the form's lines (M4d: this week, the plan for the coming weeks, and the item lists)
        /// and the analysis list. After a refused save, <paramref name="posted"/> is shown again.
        /// </summary>
        private async Task<bool> LoadFollowUpFormAsync(int enrollmentId, int? followUpId, RecordFollowUpDto? posted = null)
        {
            var enrollment = await _nutritionService.GetEnrollmentByIdAsync(enrollmentId);
            if (!enrollment.IsSuccess || enrollment.Data is null) return false;
            var form = await _nutritionService.GetFollowUpFormAsync(enrollmentId, followUpId, posted);
            if (!form.IsSuccess || form.Data is null) return false;
            var analyses = await _analysisService.GetAnalysisTypesAsync();
            ViewBag.Enrollment = enrollment.Data;
            ViewBag.Form = form.Data;
            ViewBag.AnalysisTypes = analyses.Data ?? Enumerable.Empty<SafyaClinic.Application.DTOs.Analysis.AnalysisTypeDto>();
            ViewBag.WeekNumber = form.Data.Week;
            ViewBag.FollowUpId = followUpId;
            ViewBag.IsEdit = followUpId.HasValue;
            return true;
        }

        private int NextWeek(PatientEnrollmentDto enrollment) =>
            enrollment.WeeklyFollowUps.Count == 0 ? 1 : enrollment.WeeklyFollowUps.Max(f => f.WeekNumber) + 1;

        [HttpGet]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> RecordFollowUp(int enrollmentId)
        {
            var enrollment = await _nutritionService.GetEnrollmentByIdAsync(enrollmentId);
            if (!enrollment.IsSuccess || enrollment.Data is null) { Error("Enrollment not found."); return RedirectToAction(nameof(Packages)); }
            var week = NextWeek(enrollment.Data);
            if (enrollment.Data.Status != "Active" || week > 4)
            {
                Error(enrollment.Data.Status != "Active"
                    ? "Follow-ups can only be recorded while the enrollment is active."
                    : "All 4 weekly follow-ups are already recorded for this enrollment.");
                return RedirectToAction(nameof(EnrollmentDetails), new { id = enrollmentId });
            }
            await LoadFollowUpFormAsync(enrollmentId, null);
            return View(new RecordFollowUpDto { FollowUpId = enrollmentId, FollowUpDate = _clock.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> RecordFollowUp(int enrollmentId, RecordFollowUpDto model)
        {
            var actualEnrollmentId = enrollmentId > 0 ? enrollmentId : model.FollowUpId;
            if (actualEnrollmentId <= 0)
            {
                Error("Invalid enrollment.");
                return RedirectToAction(nameof(Packages));
            }

            async Task<IActionResult> Again()
            {
                await LoadFollowUpFormAsync(actualEnrollmentId, null, model);
                return View(model);
            }

            if (!ModelState.IsValid) return await Again();
            var result = await _nutritionService.RecordFollowUpAsync(actualEnrollmentId, model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); return await Again(); }
            return RedirectWithSuccess(
                $"Week {result.Data!.WeekNumber} follow-up recorded.",
                nameof(EnrollmentDetails), routeValues: new { id = actualEnrollmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> CompleteFollowUp(int id, int enrollmentId)
        {
            var result = await _nutritionService.CompleteFollowUpAsync(id);
            if (result.IsSuccess) Success("Follow-up marked as completed.");
            else Error(result.Errors.FirstOrDefault() ?? "The follow-up could not be completed.");
            return RedirectToAction(nameof(FollowUpDetails), new { id });
        }

        [HttpGet]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> EditFollowUp(int id)
        {
            var result = await _nutritionService.GetFollowUpByIdAsync(id);
            if (!result.IsSuccess || result.Data is null) { Error("Follow-up not found."); return RedirectToAction(nameof(Packages)); }

            var dto = result.Data;
            var model = new RecordFollowUpDto
            {
                FollowUpId = dto.EnrollmentId,
                FollowUpDate = dto.FollowUpDate,
                WeightKg = dto.WeightKg,
                HeightCm = dto.HeightCm,
                BodyFatPercent = dto.BodyFatPercent,
                MuscleMassKg = dto.MuscleMassKg,
                WaistCircumferenceCm = dto.WaistCircumferenceCm,
                BloodPressureSys = dto.BloodPressureSys,
                BloodPressureDia = dto.BloodPressureDia,
                LabResultsSummary = dto.LabResultsSummary,
                DoctorNotes = dto.DoctorNotes,
                DietCompliance = dto.DietCompliance,
                SideEffects = dto.SideEffects,
                NextWeekAdjustments = dto.NextWeekAdjustments,
                LabResults = dto.LabResults.Select(l => new RecordLabResultDto
                {
                    AnalysisTypeId = l.AnalysisTypeId, ResultValue = l.ResultValue, ReferenceRange = l.ReferenceRange,
                    IsNormal = l.IsNormal, Notes = l.Notes
                }).ToList(),
            };

            await LoadFollowUpFormAsync(dto.EnrollmentId, id);
            return View("RecordFollowUp", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> EditFollowUp(int id, RecordFollowUpDto model)
        {
            var current = await _nutritionService.GetFollowUpByIdAsync(id);
            if (!current.IsSuccess || current.Data is null) { Error("Follow-up not found."); return RedirectToAction(nameof(Packages)); }
            var enrollmentId = current.Data.EnrollmentId;   // from the database, not the form

            if (ModelState.IsValid)
            {
                var result = await _nutritionService.UpdateFollowUpAsync(id, model, CurrentUserId);
                if (result.IsSuccess)
                    return RedirectWithSuccess($"Week {current.Data.WeekNumber} follow-up updated.",
                        nameof(FollowUpDetails), routeValues: new { id });
                ApplyErrors(result);
            }
            await LoadFollowUpFormAsync(enrollmentId, id, model);
            return View("RecordFollowUp", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(Permissions.Nutrition.Manage)]
        public async Task<IActionResult> DeleteFollowUp(int id, int enrollmentId)
        {
            var result = await _nutritionService.DeleteFollowUpAsync(id);
            if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "The follow-up could not be deleted.");
            else Success(string.IsNullOrEmpty(result.Message) ? "Follow-up deleted." : result.Message);

            return RedirectToAction(nameof(EnrollmentDetails), new { id = enrollmentId });
        }

        // ── Injection and vitamin types ──────────────────────────
        // M5a: edited in the one catalogue editor (Settings → Catalogues); the old addresses land there.

        [RequirePermission(Permissions.Settings.Lookups)]
        public IActionResult InjectionTypes() => RedirectToAction("Catalogue", "Lookups", new { kind = "Injection" });

        [RequirePermission(Permissions.Settings.Lookups)]
        public IActionResult VitaminTypes() => RedirectToAction("Catalogue", "Lookups", new { kind = "Vitamin" });
    }
}
