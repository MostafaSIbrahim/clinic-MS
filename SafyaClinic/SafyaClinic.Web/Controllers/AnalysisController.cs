using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Analysis;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Services.Files;

namespace SafyaClinic.Web.Controllers
{
    [Authorize(Policy = "DoctorOrAdmin")]
    public class AnalysisController : BaseController
    {
        private readonly IAnalysisService _analysisService;
        private readonly IPatientService _patientService;
        private readonly IFileStorage _files;

        public AnalysisController(
            IAnalysisService analysisService,
            IPatientService patientService,
            IFileStorage files)
        {
            _analysisService = analysisService;
            _patientService = patientService;
            _files = files;
        }

        // ── Global list (fixes the broken "/Analysis" sidebar link and gives
        //    every analysis result a discoverable list page, searchable by
        //    patient name or analysis type) ──────────────────────────────
        public async Task<IActionResult> Index([FromQuery] PaginationRequest request, string? status)
        {
            // Search-first: opening or clearing this index must not query the analysis list/count.
            status = status?.Trim();
            ViewBag.Search = request.Search;
            ViewBag.Status = status;
            var hasSearch = !string.IsNullOrWhiteSpace(request.Search) || !string.IsNullOrWhiteSpace(status);
            ViewBag.HasSearch = hasSearch;
            // Unknown statuses must not be silently ignored and broaden the query.
            if (!string.IsNullOrWhiteSpace(status)
                && (!Enum.TryParse<SafyaClinic.Domain.Enums.AnalysisStatus>(status, out var parsedStatus)
                    || !Enum.IsDefined(parsedStatus)))
                ModelState.AddModelError(nameof(status), "Select a valid analysis status.");
            if (!ModelState.IsValid || !hasSearch)
                return View(new PagedResult<MedicalAnalysisDto> { Page = 1, PageSize = request.PageSize });

            var result = await _analysisService.SearchAnalysesAsync(request, status);
            // Keep service failures distinct from successful searches with no matches.
            if (!result.IsSuccess)
                ModelState.AddModelError("", "Could not load analyses. Please try the search again.");
            return View(result.IsSuccess ? result.Data : new PagedResult<MedicalAnalysisDto>());
        }

        // ── Per-patient list ("Analyses" quick link on Patients/Details) ──
        public async Task<IActionResult> PatientAnalyses(int patientId)
        {
            var result = await _analysisService.GetPatientAnalysesAsync(patientId);
            ViewBag.PatientId = patientId;
            return View(result.IsSuccess ? result.Data : Enumerable.Empty<MedicalAnalysisDto>());
        }

        public async Task<IActionResult> Details(int id)
        {
            var result = await _analysisService.GetAnalysisByIdAsync(id);
            if (!result.IsSuccess) return RedirectToAction("Index", "Patients");
            return View(result.Data);
        }

        // ── Request (now supports selecting several analysis types at once) ──

        [HttpGet]
        [ActionName("Request")]
        public async Task<IActionResult> GetRequest(int patientId, int? recordId)
        {
            var types = await _analysisService.GetAnalysisTypesAsync();
            ViewBag.AnalysisTypes = types.Data;
            return View(new RequestAnalysisBatchRequest
            {
                PatientId = patientId,
                DoctorId = CurrentUserId,
                RecordId = recordId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Request")]
        public async Task<IActionResult> PostRequest(RequestAnalysisBatchRequest model)
        {
            if (!ModelState.IsValid || model.AnalysisTypeIds is null || !model.AnalysisTypeIds.Any())
            {
                if (model.AnalysisTypeIds is null || !model.AnalysisTypeIds.Any())
                    ModelState.AddModelError(string.Empty, "Select at least one analysis type.");
                var types = await _analysisService.GetAnalysisTypesAsync();
                ViewBag.AnalysisTypes = types.Data;
                return View(model);
            }

            var result = await _analysisService.RequestAnalysesAsync(model, CurrentUserId);
            if (!result.IsSuccess)
            {
                ApplyErrors(result);
                var types = await _analysisService.GetAnalysisTypesAsync();
                ViewBag.AnalysisTypes = types.Data;
                return View(model);
            }

            Success($"{result.Data!.Count()} analysis request(s) registered.");
            // Send the doctor straight to a printable slip listing every analysis
            // just requested, so it can be handed / printed for the patient.
            var ids = string.Join(",", result.Data!.Select(a => a.Id));
            return RedirectToAction(nameof(PrintRequestSlip), new { ids });
        }

        // ── Printable request slip for one or more analyses ────────────
        [HttpGet]
        public async Task<IActionResult> PrintRequestSlip(string ids)
        {
            var analysisIds = (ids ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            var analyses = new List<MedicalAnalysisDto>();
            foreach (var id in analysisIds)
            {
                var result = await _analysisService.GetAnalysisByIdAsync(id);
                if (result.IsSuccess) analyses.Add(result.Data!);
            }

            if (!analyses.Any()) return RedirectToAction("Index", "Patients");

            var patientResult = await _patientService.GetPatientByIdAsync(analyses.First().PatientId);
            ViewBag.Patient = patientResult.IsSuccess ? patientResult.Data : null;

            return View(analyses);
        }

        // ── Reprint a request slip for a patient's already-requested analyses ──
        [HttpGet]
        public async Task<IActionResult> PrintPatientRequests(int patientId, string status = "Requested")
        {
            var result = await _analysisService.GetPatientAnalysesAsync(patientId);
            var ids = result.IsSuccess
                ? result.Data!.Where(a => string.IsNullOrEmpty(status) || a.Status == status)
                    .Select(a => a.Id)
                : Enumerable.Empty<int>();

            if (!ids.Any())
            {
                Error($"No analyses with status '{status}' to print.");
                return RedirectToAction(nameof(PatientAnalyses), new { patientId });
            }

            return RedirectToAction(nameof(PrintRequestSlip), new { ids = string.Join(",", ids) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, UpdateAnalysisStatusRequest model)
        {
            await _analysisService.UpdateStatusAsync(id, model);
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ClinicalStaff")]
        public async Task<IActionResult> UploadAttachment(int analysisId, IFormFile? file)
        {
            var (stored, error) = await _files.SaveAsync(file, LocalFileStorage.Analysis);
            if (stored is null)
            {
                Error(error!);
                return RedirectToAction(nameof(Details), new { id = analysisId });
            }

            try
            {
                var result = await _analysisService.AddAttachmentAsync(
                    analysisId, stored.Key, stored.FileName, stored.ContentType, stored.Length, CurrentUserId);

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

            return RedirectToAction(nameof(Details), new { id = analysisId });
        }

        // ── View (inline, for the pop-up preview) vs Download (forced) ────
        [HttpGet]
        public Task<IActionResult> ViewAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: false);

        [HttpGet]
        public Task<IActionResult> DownloadAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: true);

        private async Task<IActionResult> ServeAttachmentAsync(int attachmentId, bool download)
        {
            var result = await _analysisService.GetAttachmentAsync(attachmentId);
            if (!result.IsSuccess) return NotFound();

            return AttachmentResult(_files.Find(result.Data!.FilePath), download);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId, int analysisId)
        {
            var attachment = await _analysisService.GetAttachmentAsync(attachmentId);
            var result = await _analysisService.DeleteAttachmentAsync(attachmentId);
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            return RedirectToAction(nameof(Details), new { id = analysisId });
        }
    }
}
