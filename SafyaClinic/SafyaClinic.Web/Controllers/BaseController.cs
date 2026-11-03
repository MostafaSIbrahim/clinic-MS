using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using System.Security.Claims;
using SafyaClinic.Web.Services.Files;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers
{
    public abstract class BaseController : Controller
    {
        // ── Identity ─────────────────────────────────────────────

        protected int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        // F4b: what a user may do is a permission from their groups.
        protected Task<bool> CanAsync(string permission) =>
            HttpContext.RequestServices.GetRequiredService<ICurrentPermissions>().HasAsync(permission);

        // Which clinician's day the dashboard shows; moves to ClinicianType in F4c.
        protected bool IsDoctor => User.IsInRole("Doctor");
        protected bool IsNutritionist => User.IsInRole("Nutritionist");
        // ── TempData alerts (rendered in _Layout) ────────────────

        protected void Success(string message) =>
            TempData["Success"] = message;

        protected void Error(string message) =>
            TempData["Error"] = message;

        protected void Warning(string message) =>
            TempData["Warning"] = message;
        // ── Attachments (S3) ────────────────────────────────────

        // Streams a stored attachment. Only PDF/JPG/PNG are shown inline;
        // anything else is downloaded. nosniff stops browsers guessing a type.
        protected IActionResult AttachmentResult(FileToServe? file, bool download)
        {
            if (file is null) return NotFound();

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return download || !file.Inline
                ? PhysicalFile(file.FullPath, file.ContentType, file.DownloadName)
                : PhysicalFile(file.FullPath, file.ContentType);
        }
        // ── ServiceResult → redirect helpers ────────────────────

        protected IActionResult RedirectWithSuccess(string message, string action,
            string? controller = null, object? routeValues = null)
        {
            Success(message);
            return controller is null
                ? RedirectToAction(action, routeValues)
                : RedirectToAction(action, controller, routeValues);
        }

        protected void ApplyErrors<T>(ServiceResult<T> result)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e);
        }

        protected void ApplyErrors(ServiceResult result)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e);
        }
    }
 }
