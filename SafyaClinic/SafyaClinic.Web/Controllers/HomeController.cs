namespace SafyaClinic.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Web.Models;

public class HomeController : BaseController
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        // Redirect authenticated users to Dashboard
        if (User.Identity != null && User.Identity?.IsAuthenticated == true)
        { return RedirectToAction("Index", "Dashboard"); }

        return View();
    }
    // Shown by UseExceptionHandler. Never displays exception details; the
    // reference matches the RequestId in the server log for this request.
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}