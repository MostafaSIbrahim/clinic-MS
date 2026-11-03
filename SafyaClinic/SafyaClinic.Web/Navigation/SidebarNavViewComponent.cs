using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Navigation;

/// <summary>F7: renders the sidebar from <see cref="NavigationCatalogue"/> for the signed-in user.</summary>
public class SidebarNavViewComponent : ViewComponent
{
    private readonly ICurrentPermissions _permissions;

    public SidebarNavViewComponent(ICurrentPermissions permissions) => _permissions = permissions;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in NavigationCatalogue.RelevantPermissions)
        {
            if (await _permissions.HasAsync(code)) granted.Add(code);
        }

        var route = ViewContext.RouteData.Values;
        return View(NavigationCatalogue.For(granted, route["controller"]?.ToString(), route["action"]?.ToString()));
    }
}
