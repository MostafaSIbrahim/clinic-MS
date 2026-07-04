using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SafyaClinic.Application.DTOs.Analysis;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
namespace SafyaClinic.Web.Controllers
{
    [Authorize(Policy = "DoctorOrAdmin")]
    public class AnalysisController : BaseController
    {
        private readonly IAnalysisService _analysisService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public AnalysisController(
            IAnalysisService analysisService,
            IConfiguration config,
            IWebHostEnvironment env)
        {
            _analysisService = analysisService;
            _config = config;
            _env = env;
        }

        // ── Global list (fixes the broken "/Analysis" sidebar link and gives
        //    every analysis result a discoverable list page, searchable by
        //    patient name or analysis type) ──────────────────────────────
        public async Task<IActionResult> Index([FromQuery] PaginationRequest request, string? status)
        {
            var result = await _analysisService.SearchAnalysesAsync(request, status);
            ViewBag.Search = request.Search;
            ViewBag.Status = status;
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
        public async Task<IActionResult> Request(int patientId, int? recordId)
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
        public async Task<IActionResult> Request(RequestAnalysisBatchRequest model)
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

        // ── Printable request slip for one or more freshly-requested analyses ──
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
            return View(analyses);
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
        public async Task<IActionResult> UploadAttachment(int analysisId, IFormFile file)
        {
            if (file is null || file.Length == 0)
            {
                Error("No file selected.");
                return RedirectToAction(nameof(Details), new { id = analysisId });
            }

            var basePath = _config["FileStorage:BasePath"] ?? "wwwroot/uploads";
            var folder = Path.Combine(basePath, "analysis");
            Directory.CreateDirectory(folder);
            var savedName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(folder, savedName);
            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            await _analysisService.AddAttachmentAsync(
                analysisId, fullPath, savedName, file.ContentType, file.Length, CurrentUserId);

            return RedirectToAction(nameof(Details), new { id = analysisId });
        }

        // ── View / download a result attachment ───────────────────────────
        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var result = await _analysisService.GetAttachmentAsync(attachmentId);
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
            // inline lets images/PDFs render straight in the browser; anything
            // else (e.g. .docx) still downloads with its original file name.
            return File(bytes, contentType, attachment.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId, int analysisId)
        {
            await _analysisService.DeleteAttachmentAsync(attachmentId);
            return RedirectToAction(nameof(Details), new { id = analysisId });
        }
    }
}
