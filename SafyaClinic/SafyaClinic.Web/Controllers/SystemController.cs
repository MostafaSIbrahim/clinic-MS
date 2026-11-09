using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Security;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Security;
using SafyaClinic.Web.Startup;

namespace SafyaClinic.Web.Controllers;

/// <summary>D2a: is the site ready for real patients? Admin only (user administration).</summary>
[RequirePermission(Permissions.Users.Manage)]
public class SystemController : BaseController
{
    private readonly SafyaDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly IClinicClock _clock;

    public SystemController(SafyaDbContext db, IWebHostEnvironment env, IConfiguration config, IClinicClock clock)
    {
        _db = db;
        _env = env;
        _config = config;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> Readiness()
    {
        var facts = await Startup.Readiness.GatherAsync(_db, _env, _config, _clock, Request.IsHttps);
        return View(Startup.Readiness.Evaluate(facts));
    }
}
