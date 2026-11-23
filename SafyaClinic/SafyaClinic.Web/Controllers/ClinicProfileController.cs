using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Settings;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

/// <summary>
/// M5d: the clinic profile printed on prescriptions and lab slips, edited by whoever manages clinics
/// (Admin and Clinic admin). Until it is first saved, the form starts from the ClinicInfo server setting.
/// </summary>
[RequirePermission(Permissions.Settings.Clinics)]
public class ClinicProfileController : BaseController
{
    private readonly IClinicProfileService _profile;

    public ClinicProfileController(IClinicProfileService profile) => _profile = profile;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var p = await _profile.GetAsync();
        ViewBag.Profile = p;
        return View(new ClinicProfileForm
        {
            NameEn = p.NameEn, NameAr = p.NameAr, TaglineEn = p.TaglineEn, TaglineAr = p.TaglineAr,
            AddressEn = p.AddressEn, AddressAr = p.AddressAr, Phone = p.Phone, SecondPhone = p.SecondPhone,
            Email = p.Email, LicenceLine = p.LicenceLine,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ClinicProfileForm form)
    {
        foreach (var (field, message) in ClinicProfileService.Check(form))
            ModelState.AddModelError(field, message);
        if (ModelState.IsValid)
        {
            var result = await _profile.SaveAsync(form, CurrentUserId);
            if (result.IsSuccess) return RedirectWithSuccess(result.Message, nameof(Index));
            ApplyErrors(result);
        }
        ViewBag.Profile = await _profile.GetAsync();
        return View(form);
    }
}
