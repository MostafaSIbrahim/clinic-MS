using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.DTOs.Common;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class UsersController : BaseController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) =>
        _userService = userService;

    public async Task<IActionResult> Index(PaginationRequest request)
    {
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
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _userService.CreateUserAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }

        return RedirectWithSuccess("User created successfully.", nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, bool isActive)
    {
        await _userService.SetUserActiveAsync(id, isActive);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(int userId, int roleId)
    {
        await _userService.AssignRoleAsync(userId, roleId, CurrentUserId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(int userId, int roleId)
    {
        await _userService.RemoveRoleAsync(userId, roleId);
        return RedirectToAction(nameof(Index));
    }
}