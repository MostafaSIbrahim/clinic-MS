using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using System.Security.Claims;
namespace SafyaClinic.Web.Controllers
{
    public abstract class BaseController : Controller
    {
        // ── Identity ─────────────────────────────────────────────

        protected int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        protected bool IsAdmin => User.IsInRole("Admin");
        protected bool IsDoctor => User.IsInRole("Doctor");
        protected bool IsNutritionist => User.IsInRole("Nutritionist");
        protected bool IsReception => User.IsInRole("Reception");

        // ── TempData alerts (rendered in _Layout) ────────────────

        protected void Success(string message) =>
            TempData["Success"] = message;

        protected void Error(string message) =>
            TempData["Error"] = message;

        protected void Warning(string message) =>
            TempData["Warning"] = message;

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
