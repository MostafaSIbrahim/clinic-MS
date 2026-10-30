using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.DTOs.Common;
// Role lookup: read available roles from the existing catalog instead of fixed IDs in views.
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Models;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class UsersController : BaseController
{
    private readonly IUserService _userService;

    private readonly SafyaDbContext _context;

    public UsersController(IUserService userService, SafyaDbContext context)
    {
        _userService = userService;
        _context = context;
    }

    private async Task LoadRolesAsync() => ViewBag.AllRoles = await _context.Roles.AsNoTracking()
        .OrderBy(r => r.RoleName)
        .Select(r => new RoleLookupItem(r.Id, r.RoleName, r.Description)).ToListAsync();

    public async Task<IActionResult> Index(PaginationRequest request)
    {
        // Role lookup: use real role IDs for both assignment and removal.
        await LoadRolesAsync();
        var result = await _userService.GetAllUsersAsync(request);

        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            return View(new PagedResult<UserDto>
            {
                Items = [],
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRolesAsync();
        return View(new CreateUserRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest model)
    {
        // Role lookup: preserve the complete choices when validation returns the form.
        await LoadRolesAsync();
        if (!ModelState.IsValid) return View(model);

        var result = await _userService.CreateUserAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }

        return RedirectWithSuccess("User created successfully.", nameof(Index));
    }


    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (!user.IsSuccess) return NotFound();

        ViewBag.UserName = user.Data!.FullName;
        return View(new ResetPasswordRequest { UserId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest model)
    {
        var result = await _userService.ResetPasswordAsync(model);
        if (!result.IsSuccess)
        {
            ViewBag.UserName = (await _userService.GetUserByIdAsync(model.UserId)).Data?.FullName;
            ApplyErrors(result);
            return View(model);
        }

        return RedirectWithSuccess(result.Message, nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, bool isActive)
    {
        var result = await _userService.SetUserActiveAsync(id, isActive, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not change the user's status.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(int userId, int roleId)
    {
        // Role lookup: show service failures instead of silently returning to the list.
        var result = await _userService.AssignRoleAsync(userId, roleId, CurrentUserId);
        if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "Could not assign role.");
        else Success("Role assigned.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(int userId, int roleId)
    {
        // Role lookup: surface stale or invalid role removals to the administrator.
        var result = await _userService.RemoveRoleAsync(userId, roleId);
        if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "Could not remove role.");
        else Success("Role removed.");
        return RedirectToAction(nameof(Index));
    }
}
