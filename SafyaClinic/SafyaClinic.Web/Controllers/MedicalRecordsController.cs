using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers
{
    [Authorize(Policy = "DoctorOrAdmin")]
    public class MedicalRecordsController : BaseController
    {
        private readonly IPatientRecordService _recordService;
        private readonly IAnalysisService _analysisService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public MedicalRecordsController(
            IPatientRecordService recordService,
            IAnalysisService analysisService,
            IConfiguration config,
            IWebHostEnvironment env)
        {
            _recordService = recordService;
            _analysisService = analysisService;
            _config = config;
            _env = env;
        }

        public async Task<IActionResult> PatientRecords(int patientId)
        {
            var result = await _recordService.GetPatientRecordsAsync(patientId);
            ViewBag.PatientId = patientId;
            return View(result.IsSuccess ? result.Data : Enumerable.Empty<PatientRecordDto>());
        }

        public async Task<IActionResult> Details(int id)
        {
            var result = await _recordService.GetRecordByIdAsync(id);
            if (!result.IsSuccess) return RedirectToAction("Index", "Patients");

            var analyses = await _analysisService.GetAnalysesByRecordAsync(id);
            ViewBag.Analyses = analyses.IsSuccess ? analyses.Data : Enumerable.Empty<SafyaClinic.Application.DTOs.Analysis.MedicalAnalysisDto>();

            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int patientId, int? reservationId)
        {
            var types = await _recordService.GetTreatmentTypesAsync();
            ViewBag.TreatmentTypes = types.Data;
            return View(new CreatePatientRecordRequest
            {
                PatientId = patientId,
                DoctorId = CurrentUserId,
                ReservationId = reservationId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePatientRecordRequest model)
        {
            if (!ModelState.IsValid)
            {
                var types = await _recordService.GetTreatmentTypesAsync();
                ViewBag.TreatmentTypes = types.Data;
                return View(model);
            }
            var result = await _recordService.CreateRecordAsync(model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess("Record created.", nameof(Details), routeValues: new { id = result.Data!.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _recordService.GetRecordByIdAsync(id);
            if (!result.IsSuccess) return RedirectToAction(nameof(PatientRecords));
            var r = result.Data!;
            if (r.IsLocked) { Error("This record is locked."); return RedirectToAction(nameof(Details), new { id }); }
            return View(new UpdatePatientRecordRequest
            {
                ChiefComplaint = r.ChiefComplaint,
                PresentIllnessHistory = r.PresentIllnessHistory,
                Diagnosis = r.Diagnosis,
                DifferentialDiagnosis = r.DifferentialDiagnosis,
                TreatmentPlan = r.TreatmentPlan,
                Notes = r.Notes,
                FollowUpDate = r.FollowUpDate
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdatePatientRecordRequest model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await _recordService.UpdateRecordAsync(id, model);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess("Record updated.", nameof(Details), routeValues: new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Lock(int id)
        {
            await _recordService.LockRecordAsync(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Treatments ────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTreatment(int recordId, AddTreatmentRequest model)
        {
            var result = await _recordService.AddTreatmentAsync(recordId, model, CurrentUserId);
            if (!result.IsSuccess) Error(result.Errors.First());
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTreatment(int treatmentId, int recordId)
        {
            await _recordService.RemoveTreatmentAsync(treatmentId);
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── Prescriptions ─────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescription(int recordId, AddPrescriptionRequest model)
        {
            var result = await _recordService.AddPrescriptionAsync(recordId, model, CurrentUserId);
            if (!result.IsSuccess) Error(result.Errors.First());
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPrescriptionPrinted(int prescriptionId, int recordId)
        {
            await _recordService.MarkPrescriptionPrintedAsync(prescriptionId);
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── Attachments ───────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ClinicalStaff")]
        public async Task<IActionResult> UploadPrescriptionAttachment(
            int prescriptionId, int recordId, IFormFile file)
        {
            if (file is null || file.Length == 0)
            {
                Error("No file selected.");
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var (filePath, savedName) = await SaveFileAsync(file, "prescriptions");
            await _recordService.AddPrescriptionAttachmentAsync(
                prescriptionId, filePath, savedName,
                file.ContentType, file.Length, CurrentUserId);

            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId, int recordId)
        {
            await _recordService.DeleteAttachmentAsync(attachmentId);
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── View / download a prescription attachment ─────────────
        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var result = await _recordService.GetAttachmentAsync(attachmentId);
            if (!result.IsSuccess)
            {
                Error("Attachment not found.");
                return RedirectToAction("Index", "Patients");
            }

            var attachment = result.Data!;
            var fullPath = Path.IsPathRooted(attachment.FilePath)
                ? attachment.FilePath
                : Path.Combine(_env.ContentRootPath, attachment.FilePath);

            if (!System.IO.File.Exists(fullPath))
            {
                Error("The attachment file could not be found on disk.");
                return RedirectToAction("Index", "Patients");
            }

            var contentType = attachment.ContentType;
            if (string.IsNullOrWhiteSpace(contentType))
            {
                var provider = new FileExtensionContentTypeProvider();
                if (!provider.TryGetContentType(fullPath, out contentType))
                    contentType = "application/octet-stream";
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, contentType, attachment.FileName);
        }

        private async Task<(string filePath, string savedName)> SaveFileAsync(
            IFormFile file, string subFolder)
        {
            var basePath = _config["FileStorage:BasePath"] ?? "wwwroot/uploads";
            var folder = Path.Combine(basePath, subFolder);
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(file.FileName);
            var savedName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(folder, savedName);
            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return (fullPath, savedName);
        }
    }
}
