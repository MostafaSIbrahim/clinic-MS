using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Models;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

/// <summary>
/// F8c: the component sample page. Every shared component on one page, with sample data,
/// so each can be checked (axe, keyboard, phone width, right-to-left) in isolation. Admin only.
/// </summary>
[RequirePermission(Permissions.Users.Manage)]
public class ComponentsController : BaseController
{
    /// <param name="errors">Shows the form fields in their error state.</param>
    [HttpGet]
    public IActionResult Index(bool errors = false)
    {
        var model = new ComponentSampleModel();
        if (errors)
        {
            ModelState.AddModelError(nameof(ComponentSampleModel.FullName), "Enter the patient's full name.");
            ModelState.AddModelError(nameof(ComponentSampleModel.ClinicId), "Choose a clinic.");
            model.FullName = null;
        }
        return View(model);
    }
}
