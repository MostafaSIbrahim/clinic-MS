using System.ComponentModel.DataAnnotations;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Models;

/// <summary>F4d: what the group form posts.</summary>
public class PermissionGroupFormModel
{
    [Display(Name = "Group name")]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public List<string>? PermissionCodes { get; set; }
}

/// <summary>F4d: the group page — details, permissions by area, members.</summary>
public class PermissionGroupEditModel : PermissionGroupFormModel
{
    public PermissionGroupDetail Group { get; set; } = null!;
    public IReadOnlySet<string> Selected { get; set; } = new HashSet<string>();
    public IReadOnlyList<UserChoice> UserChoices { get; set; } = [];
}
