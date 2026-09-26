using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Models;

namespace SafyaClinic.Web.Controllers;

// Role lookup: protect both direct navigation and POST requests, not only the menu link.
[Authorize(Policy = "AdminOnly")]
public class RolesController : BaseController
{
    private readonly SafyaDbContext _context;
    public RolesController(SafyaDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(new RoleLookupViewModel { Roles = await LoadRolesAsync() });

    // Role lookup: add roles only; existing role names used by authorization cannot be renamed here.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleLookupViewModel model)
    {
        model.RoleName = model.RoleName?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim();
        if (string.IsNullOrWhiteSpace(model.RoleName))
            ModelState.TryAddModelError(nameof(model.RoleName), "Enter a role name.");

        if (ModelState.IsValid)
        {
            var normalizedName = model.RoleName.ToUpperInvariant();
            if (await _context.Roles.AnyAsync(r => r.RoleName.ToUpper() == normalizedName))
                ModelState.AddModelError(nameof(model.RoleName), "A role with this name already exists.");
        }

        if (ModelState.IsValid)
        {
            var role = new Role { RoleName = model.RoleName, Description = model.Description };
            _context.Roles.Add(role);
            try
            {
                await _context.SaveChangesAsync();
                return RedirectWithSuccess("Role added successfully.", nameof(Index));
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Role lookup: a concurrent duplicate submission should redisplay a validation error.
                _context.Entry(role).State = EntityState.Detached;
                ModelState.AddModelError(nameof(model.RoleName), "A role with this name already exists.");
            }
        }

        model.Roles = await LoadRolesAsync();
        return View(nameof(Index), model);
    }

    private Task<List<RoleLookupItem>> LoadRolesAsync() => _context.Roles.AsNoTracking()
        .OrderBy(r => r.RoleName)
        .Select(r => new RoleLookupItem(r.Id, r.RoleName, r.Description)).ToListAsync();
}
