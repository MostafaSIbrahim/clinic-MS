using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.Interfaces.Services;
namespace SafyaClinic.Web.Controllers
{
    [Authorize(Policy = "NutritionTeam")]
    public class NutritionController : BaseController
    {
        private readonly INutritionService _nutritionService;
        private readonly IPatientService _patientService;
        private readonly IUserService _userService;

        public NutritionController(
            INutritionService nutritionService,
            IPatientService patientService,
            IUserService userService)
        {
            _nutritionService = nutritionService;
            _patientService = patientService;
            _userService = userService;
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
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreatePackage()
        {
            await LoadCatalogViewBagsAsync();
            return View(new CreateNutritionPackageDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
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
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeactivatePackage(int id)
        {
            await _nutritionService.DeactivatePackageAsync(id);
            return RedirectWithSuccess("Package deactivated.", nameof(Packages));
        }

        // ── Enrollments ──────────────────────────────────────────

        public async Task<IActionResult> PatientEnrollments(int patientId)
        {
            var result = await _nutritionService.GetPatientEnrollmentsAsync(patientId);
            ViewBag.PatientId = patientId;
            return View(result.IsSuccess ? result.Data : Enumerable.Empty<PatientEnrollmentDto>());
        }

        public async Task<IActionResult> EnrollmentDetails(int id)
        {
            var result = await _nutritionService.GetEnrollmentByIdAsync(id);
            if (!result.IsSuccess) { Error("Enrollment not found."); return RedirectToAction(nameof(Packages)); }
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Enroll(int patientId)
        {
            var packages = await _nutritionService.GetActivePackagesAsync();
            var doctors = await _userService.GetDoctorsAsync();
            ViewBag.Packages = packages.Data;
            ViewBag.Doctors = doctors.Data;
            return View(new CreateEnrollmentDto
            {
                PatientId = patientId,
                StartDate = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(CreateEnrollmentDto model)
        {
            if (!ModelState.IsValid)
            {
                var packages = await _nutritionService.GetActivePackagesAsync();
                var doctors = await _userService.GetDoctorsAsync();
                ViewBag.Packages = packages.Data;
                ViewBag.Doctors = doctors.Data;
                return View(model);
            }
            var result = await _nutritionService.EnrollPatientAsync(model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess(
                "Patient enrolled.", nameof(EnrollmentDetails),
                routeValues: new { id = result.Data!.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEnrollmentStatus(int id, string status, int patientId)
        {
            await _nutritionService.UpdateEnrollmentStatusAsync(id, status);
            return RedirectToAction(nameof(EnrollmentDetails), new { id });
        }

        // ── Weekly Follow-Ups ────────────────────────────────────

        public async Task<IActionResult> FollowUpDetails(int id)
        {
            var result = await _nutritionService.GetFollowUpByIdAsync(id);
            if (!result.IsSuccess) { Error("Follow-up not found."); return RedirectToAction(nameof(Packages)); }
            return View(result.Data);
        }

        [HttpGet]
        public IActionResult RecordFollowUp(int enrollmentId) =>
            View(new RecordFollowUpDto { FollowUpId = enrollmentId });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordFollowUp(int enrollmentId, RecordFollowUpDto model)
        {
            var actualEnrollmentId = enrollmentId > 0 ? enrollmentId : model.FollowUpId;

            if (actualEnrollmentId <= 0)
            {
                Error("Invalid enrollment.");
                return RedirectToAction(nameof(Packages));
            }
            if (!ModelState.IsValid) return View(model);
            var result = await _nutritionService.RecordFollowUpAsync(enrollmentId, model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess(
                $"Week {result.Data!.WeekNumber} follow-up recorded.",
                nameof(EnrollmentDetails), routeValues: new { id = enrollmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteFollowUp(int id, int enrollmentId)
        {
            await _nutritionService.CompleteFollowUpAsync(id);
            return RedirectToAction(nameof(EnrollmentDetails), new { id = enrollmentId });
        }
        // In NutritionController.cs

        [HttpGet]
        public async Task<IActionResult> EditFollowUp(int id)
        {
            var result = await _nutritionService.GetFollowUpByIdAsync(id);
            if (!result.IsSuccess) { Error("Follow-up not found."); return RedirectToAction(nameof(Packages)); }

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
                // Parse BloodPressure back to Sys/Dia if stored as string
                BloodPressureSys = dto.BloodPressure != null ? int.Parse(dto.BloodPressure.Split('/')[0]) : null,
                BloodPressureDia = dto.BloodPressure != null ? int.Parse(dto.BloodPressure.Split('/')[1]) : null,
                LabResultsSummary = dto.LabResultsSummary,
                DoctorNotes = dto.DoctorNotes,
                DietCompliance = dto.DietCompliance,
                SideEffects = dto.SideEffects,
                NextWeekAdjustments = dto.NextWeekAdjustments,
                // AdministeredItems and LabResults would need separate loading
            };

            ViewBag.FollowUpId = id;
            ViewBag.IsEdit = true;
            return View("RecordFollowUp", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFollowUp(int id, RecordFollowUpDto model)
        {
            if (!ModelState.IsValid) return View("RecordFollowUp", model);

            var result = await _nutritionService.UpdateFollowUpAsync(id, model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); return View("RecordFollowUp", model); }

            return RedirectWithSuccess(
                "Follow-up updated.",
                nameof(EnrollmentDetails), routeValues: new { id = model.FollowUpId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFollowUp(int id, int enrollmentId)
        {
            var result = await _nutritionService.DeleteFollowUpAsync(id);
            if (!result.IsSuccess) Error(result.Errors.First());
            else Success("Follow-up deleted.");

            return RedirectToAction(nameof(EnrollmentDetails), new { id = enrollmentId });
        }

        // ── Injection Types (feature 5) ───────────────────────────

        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> InjectionTypes()
        {
            var result = await _nutritionService.GetInjectionTypesAsync(includeInactive: true);
            return View(result.IsSuccess ? result.Data : Enumerable.Empty<InjectionTypeDto>());
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult CreateInjectionType() => View(new CreateInjectionTypeDto());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateInjectionType(CreateInjectionTypeDto model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await _nutritionService.CreateInjectionTypeAsync(model);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess("Injection type created.", nameof(InjectionTypes));
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditInjectionType(int id)
        {
            var result = await _nutritionService.GetInjectionTypeByIdAsync(id);
            if (!result.IsSuccess) { Error("Injection type not found."); return RedirectToAction(nameof(InjectionTypes)); }
            var i = result.Data!;
            ViewBag.Id = id;
            ViewBag.IsInUse = i.IsInUse;
            return View(new UpdateInjectionTypeDto
            {
                InjectionName = i.InjectionName,
                Unit = i.Unit,
                Description = i.Description,
                DefaultDosage = i.DefaultDosage,
                IsActive = i.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditInjectionType(int id, UpdateInjectionTypeDto model)
        {
            if (!ModelState.IsValid) { ViewBag.Id = id; return View(model); }
            var result = await _nutritionService.UpdateInjectionTypeAsync(id, model);
            if (!result.IsSuccess) { ApplyErrors(result); ViewBag.Id = id; return View(model); }
            return RedirectWithSuccess("Injection type updated.", nameof(InjectionTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteInjectionType(int id)
        {
            var result = await _nutritionService.DeleteInjectionTypeAsync(id);
            if (!result.IsSuccess) Error(result.Errors.First()); else Success(result.Message);
            return RedirectToAction(nameof(InjectionTypes));
        }

        // ── Vitamin Types (feature 6) ─────────────────────────────

        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> VitaminTypes()
        {
            var result = await _nutritionService.GetVitaminTypesAsync(includeInactive: true);
            return View(result.IsSuccess ? result.Data : Enumerable.Empty<VitaminTypeDto>());
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult CreateVitaminType() => View(new CreateVitaminTypeDto());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateVitaminType(CreateVitaminTypeDto model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await _nutritionService.CreateVitaminTypeAsync(model);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess("Vitamin type created.", nameof(VitaminTypes));
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditVitaminType(int id)
        {
            var result = await _nutritionService.GetVitaminTypeByIdAsync(id);
            if (!result.IsSuccess) { Error("Vitamin type not found."); return RedirectToAction(nameof(VitaminTypes)); }
            var v = result.Data!;
            ViewBag.Id = id;
            ViewBag.IsInUse = v.IsInUse;
            return View(new UpdateVitaminTypeDto
            {
                VitaminName = v.VitaminName,
                Formulation = v.Formulation,
                Unit = v.Unit,
                Description = v.Description,
                IsActive = v.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> EditVitaminType(int id, UpdateVitaminTypeDto model)
        {
            if (!ModelState.IsValid) { ViewBag.Id = id; return View(model); }
            var result = await _nutritionService.UpdateVitaminTypeAsync(id, model);
            if (!result.IsSuccess) { ApplyErrors(result); ViewBag.Id = id; return View(model); }
            return RedirectWithSuccess("Vitamin type updated.", nameof(VitaminTypes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteVitaminType(int id)
        {
            var result = await _nutritionService.DeleteVitaminTypeAsync(id);
            if (!result.IsSuccess) Error(result.Errors.First()); else Success(result.Message);
            return RedirectToAction(nameof(VitaminTypes));
        }
    }
}
