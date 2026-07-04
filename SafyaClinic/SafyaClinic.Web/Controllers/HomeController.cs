namespace SafyaClinic.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

public class HomeController : BaseController
{
    public IActionResult Index()
    {
        // Redirect authenticated users to Dashboard
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        // Redirect guests to Login
        return RedirectToAction("Login", "Auth");
    }
    public IActionResult Error()
    {
        var msg = HttpContext.Session.GetString("GlobalError") ?? "An unexpected error occurred.";
        ViewBag.ErrorMessage = msg;
        return View();
    }
}