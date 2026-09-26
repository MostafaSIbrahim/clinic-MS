using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SafyaClinic.Web.Models;

// Role lookup: bind only the editable fields, with limits matching the Roles table.
public class RoleLookupViewModel
{
    [Required(ErrorMessage = "Enter a role name.")]
    [StringLength(50)]
    [Display(Name = "Role name")]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    [BindNever]
    public IReadOnlyList<RoleLookupItem> Roles { get; set; } = Array.Empty<RoleLookupItem>();
}

// Role lookup: share database IDs/names with user creation and role assignment controls.
public record RoleLookupItem(int Id, string Name, string? Description);
