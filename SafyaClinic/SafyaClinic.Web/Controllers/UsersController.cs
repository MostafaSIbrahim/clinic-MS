using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

[RequirePermission(Permissions.Users.Manage)]
public class UsersController : BaseController
{
    private readonly IUserService _userService;
    private readonly IPermissionGroupService _groups;

    public UsersController(IUserService userService, IPermissionGroupService groups)
    {
        _userService = userService;
        _groups = groups;
    }

    // F4d: users join permission groups (roles are retired).
    private async Task LoadGroupsAsync() => ViewBag.AllGroups = await _groups.GetGroupsAsync();

    public async Task<IActionResult> Index(PaginationRequest request)
    {
        await LoadGroupsAsync();
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
        await LoadGroupsAsync();
        return View(new CreateUserRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest model)
    {
        await LoadGroupsAsync();
        if (!ModelState.IsValid) return View(model);

        var result = await _userService.CreateUserAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }

        var problems = new List<string>();
        foreach (var groupId in model.GroupIds.Distinct())
        {
            var added = await _groups.AddMemberAsync(groupId, result.Data!.Id, CurrentUserId);
            if (!added.IsSuccess) problems.AddRange(added.Errors);
        }

        if (problems.Count > 0)
        {
            Warning("User created, but some groups were not added: " + string.Join(" ", problems));
            return RedirectToAction(nameof(Index));
        }
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
    public async Task<IActionResult> AddToGroup(int userId, int groupId)
    {
        var result = await _groups.AddMemberAsync(groupId, userId, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not add the user to the group.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromGroup(int userId, int groupId)
    {
        var result = await _groups.RemoveMemberAsync(groupId, userId, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not remove the user from the group.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetClinicianType(int userId, ClinicianType clinicianType)
    {
        var result = await _userService.SetClinicianTypeAsync(userId, clinicianType, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not change the clinician type.");
        return RedirectToAction(nameof(Index));
    }
}
