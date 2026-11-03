using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Models;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

/// <summary>F4d (D1): admins create groups, choose their permissions and add members.</summary>
[RequirePermission(Permissions.Users.Manage)]
public class PermissionGroupsController : BaseController
{
    private readonly IPermissionGroupService _groups;

    public PermissionGroupsController(IPermissionGroupService groups) => _groups = groups;

    public async Task<IActionResult> Index() => View(await _groups.GetGroupsAsync());

    [HttpGet]
    public IActionResult Create() => View(new PermissionGroupFormModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PermissionGroupFormModel model)
    {
        var result = await _groups.CreateGroupAsync(model.Name ?? string.Empty, model.Description, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            return View(model);
        }

        return RedirectWithSuccess(result.Message, nameof(Edit), routeValues: new { id = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _groups.GetGroupAsync(id);
        if (group is null) return NotFound();
        return View(await EditModelAsync(group, posted: null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PermissionGroupFormModel model)
    {
        var result = await _groups.UpdateGroupAsync(
            id, model.Name ?? string.Empty, model.Description, model.PermissionCodes ?? new List<string>(), CurrentUserId);
        if (result.IsSuccess)
            return RedirectWithSuccess(result.Message, nameof(Edit), routeValues: new { id });

        ApplyErrors(result);
        var group = await _groups.GetGroupAsync(id);
        if (group is null) return NotFound();
        return View(await EditModelAsync(group, model));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _groups.DeleteGroupAsync(id, CurrentUserId);
        if (result.IsSuccess) return RedirectWithSuccess(result.Message, nameof(Index));

        Error(result.Errors.FirstOrDefault() ?? "Could not delete the group.");
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, int userId)
    {
        var result = await _groups.AddMemberAsync(id, userId, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not add the member.");
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        var result = await _groups.RemoveMemberAsync(id, userId, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else Error(result.Errors.FirstOrDefault() ?? "Could not remove the member.");
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<PermissionGroupEditModel> EditModelAsync(PermissionGroupDetail group, PermissionGroupFormModel? posted) =>
        new()
        {
            Group = group,
            Name = posted?.Name ?? group.Name,
            Description = posted is null ? group.Description : posted.Description,
            Selected = posted is null || group.IsAdministrators
                ? group.Permissions
                : (posted.PermissionCodes ?? new List<string>()).ToHashSet(StringComparer.Ordinal),
            UserChoices = await _groups.GetUserChoicesAsync()
        };
}
