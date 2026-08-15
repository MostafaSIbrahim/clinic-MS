namespace SafyaClinic.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public IActionResult Error()
    {
        var msg = HttpContext.Session.GetString("GlobalError") ?? "An unexpected error occurred.";
        ViewBag.ErrorMessage = msg;
        return View();
    }
}