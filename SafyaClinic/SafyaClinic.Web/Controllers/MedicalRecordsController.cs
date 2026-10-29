using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Services.Files;

namespace SafyaClinic.Web.Controllers
{
    [Authorize(Policy = "DoctorOrAdmin")]
    public class MedicalRecordsController : BaseController
    {
        private readonly IPatientRecordService _recordService;
        private readonly IAnalysisService _analysisService;
        private readonly IConfiguration _config;
        private readonly IFileStorage _files;
        private readonly IReservationService _reservationService;
        private readonly IPaymentService _paymentService;

        public MedicalRecordsController(
            IPatientRecordService recordService,
            IAnalysisService analysisService,
            IConfiguration config,
            IFileStorage files,
            IReservationService reservationService,
            IPaymentService paymentService)
        {
            _recordService = recordService;
            _analysisService = analysisService;
            _config = config;
            _files = files;
            _reservationService = reservationService;
            _paymentService = paymentService;
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

            if (!result.IsSuccess || result.Data is null)
                return RedirectToAction("Index", "Patients");

            var record = result.Data;

            ViewBag.CanCompleteConsultation = false;

            if (record.ReservationId.HasValue)
            {
                var contextResult =
                    await _reservationService.GetConsultationContextAsync(
                        record.ReservationId.Value,
                        CurrentUserId,
                        IsAdmin);

                if (contextResult.IsSuccess && contextResult.Data is { } context)
                {
                    ViewBag.CanCompleteConsultation =
                        context.PatientRecordId == record.Id &&
                        context.PatientId == record.PatientId &&
                        context.DoctorId == record.DoctorId;
                }
            }

            var analyses = await _analysisService.GetAnalysesByRecordAsync(id);
            ViewBag.Analyses = analyses.IsSuccess
                ? analyses.Data
                : Enumerable.Empty<SafyaClinic.Application.DTOs.Analysis.MedicalAnalysisDto>();

            var prescriptions = await _recordService.GetPrescriptionsByRecordAsync(id);
            ViewBag.Prescriptions = prescriptions.IsSuccess
                ? prescriptions.Data
                : Enumerable.Empty<PrescriptionListDto>();

            return View(record);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int patientId, int? reservationId)
        {
            if (reservationId.HasValue)
            {
                var result = await _reservationService.GetConsultationContextAsync(
                    reservationId.Value,
                    CurrentUserId,
                    IsAdmin);

                if (!result.IsSuccess ||
                    result.Data is null ||
                    result.Data.PatientId != patientId)
                {
                    Error("The reservation is unavailable for this patient's consultation.");
                    return RedirectToAction("Queue", "Reservations");
                }

                return RedirectToAction(
                    nameof(Consultation),
                    new { reservationId = reservationId.Value });
            }

            return View(new CreatePatientRecordRequest
            {
                PatientId = patientId,
                DoctorId = CurrentUserId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePatientRecordRequest model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await _recordService.CreateRecordAsync(
                    model,
                    CurrentUserId,
                    IsAdmin);
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
        public async Task<IActionResult> CreatePrescription(CreatePrescriptionRequest model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await _recordService.CreatePrescriptionAsync(model, CurrentUserId);
            if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
            return RedirectWithSuccess("Prescription created.", nameof(Details), routeValues: new { id = model.RecordId });
        }
        [HttpGet]
        public async Task<IActionResult> CreatePrescription(int recordId)
        {
            return View(new CreatePrescriptionRequest { RecordId = recordId });
        }

        [HttpGet]
        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            var result = await _recordService.GetPrescriptionByIdAsync(id);
            if (!result.IsSuccess) return NotFound();
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescriptionItem(int prescriptionId, AddPrescriptionItemRequest model)
        {
            var result = await _recordService.AddPrescriptionItemAsync(prescriptionId, model);
            if (!result.IsSuccess) Error(result.Errors.First());
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePrescriptionItem(int itemId, int prescriptionId)
        {
            await _recordService.RemovePrescriptionItemAsync(itemId);
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
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
     int prescriptionId, int recordId, IFormFile? file)
        {
            var (stored, error) = await _files.SaveAsync(file, LocalFileStorage.Prescriptions);
            if (stored is null)
            {
                Error(error!);
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            try
            {
                var result = await _recordService.AddPrescriptionAttachmentAsync(
                    prescriptionId, stored.Key, stored.FileName,
                    stored.ContentType, stored.Length, CurrentUserId);

                if (!result.IsSuccess)
                {
                    _files.Delete(stored.Key);
                    Error(result.Errors.FirstOrDefault() ?? "The file could not be attached.");
                }
            }
            catch
            {
                _files.Delete(stored.Key);   // no orphan file if the database insert fails
                throw;
            }

            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId, int recordId)
        {
            var attachment = await _recordService.GetAttachmentAsync(attachmentId);
            var result = await _recordService.DeleteAttachmentAsync(attachmentId);
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── View (inline, for the pop-up preview) vs Download (forced) ────
        [HttpGet]
        public Task<IActionResult> ViewAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: false);

        [HttpGet]
        public Task<IActionResult> DownloadAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: true);

        private async Task<IActionResult> ServeAttachmentAsync(int attachmentId, bool download)
        {
            var result = await _recordService.GetAttachmentAsync(attachmentId);
            if (!result.IsSuccess) return NotFound();

            return AttachmentResult(_files.Find(result.Data!.FilePath), download);
        }
        // ── Printable prescription (dynamic paper size / design) ──────────
        [HttpGet]
        public async Task<IActionResult> PrintPrescription(
            int prescriptionId, string paperSize = "A4", string design = "classic")
        {
            var result = await _recordService.GetPrescriptionForPrintAsync(prescriptionId);
            if (!result.IsSuccess)
            {
                Error("Prescription not found.");
                return RedirectToAction("Index", "Patients");
            }

            ViewBag.PaperSize = paperSize;
            ViewBag.Design = design;
            ViewBag.ClinicName = _config["ClinicInfo:Name"] ?? "Clinic";
            ViewBag.ClinicTagline = _config["ClinicInfo:Tagline"];
            ViewBag.ClinicAddress = _config["ClinicInfo:Address"];
            ViewBag.ClinicPhone = _config["ClinicInfo:Phone"];
            ViewBag.ClinicEmail = _config["ClinicInfo:Email"];

            return View(result.Data);
        }
        //---Consultation Context for Doctor Queue and Consultation Pages--//
        [HttpGet]
        public async Task<IActionResult> Consultation(int reservationId)
        {
            var result = await _reservationService.GetConsultationContextAsync(
                reservationId,
                CurrentUserId,
                IsAdmin);

            if (!result.IsSuccess || result.Data is null)
            {
                Error(result.Errors.FirstOrDefault()
                    ?? "Could not open consultation.");

                return RedirectToAction("Queue", "Reservations");
            }

            var context = result.Data;

            if (context.PatientRecordId.HasValue)
            {
                return RedirectToAction(
                    nameof(Details),
                    new { id = context.PatientRecordId.Value });
            }

            return View("Create", new CreatePatientRecordRequest
            {
                PatientId = context.PatientId,
                DoctorId = context.DoctorId,
                ReservationId = context.ReservationId,
                Category = context.Category
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "DoctorOrAdmin")]
        public async Task<IActionResult> CompleteConsultation(int recordId)
        {
            var result = await _reservationService.CompleteConsultationAsync(
                recordId,
                CurrentUserId,
                IsAdmin);

            if (!result.IsSuccess || result.Data is null)
            {
                Error(result.Errors.FirstOrDefault()
                    ?? "Could not complete consultation.");

                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            var reservation = result.Data;

            if (IsAdmin || IsReception)
            {
                var dueResult = await _paymentService.GetDueAmountAsync(
                    reservation.PatientId,
                    reservation.Id,
                    null);

                if (!dueResult.IsSuccess)
                {
                    Warning(
                        "Consultation completed, but the remaining balance " +
                        "could not be checked. Review the patient's payment summary.");

                    return RedirectToAction(
                        "PatientSummary",
                        "Payments",
                        new { patientId = reservation.PatientId });
                }

                if (dueResult.Data > 0m)
                {
                    Success("Consultation completed. Payment remains outstanding.");

                    return RedirectToAction(
                        "Collect",
                        "Payments",
                        new
                        {
                            patientId = reservation.PatientId,
                            reservationId = reservation.Id
                        });
                }
            }

            Success("Consultation completed.");

            return RedirectToAction("Queue", "Reservations");
        }
    }
}
