using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Security;
using Xunit;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>
/// F4b: who can open each controller action, worked out from its [Authorize] and
/// [RequirePermission] attributes. The approved list is access-matrix.txt; moving to
/// permissions (F4b-2) must leave it unchanged. Checks inside actions are not covered here.
/// </summary>
public class AccessMatrixTests
{
    private static readonly string[] Roles =
    {
        SystemGroups.AdminRole, SystemGroups.DoctorRole, SystemGroups.NutritionistRole,
        SystemGroups.ReceptionRole, SystemGroups.ReceptionAdminRole
    };

    private static IEnumerable<Type> Controllers =>
        typeof(BaseController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    public static IReadOnlyList<string> Compute()
    {
        var lines = new List<string>();
        foreach (var controller in Controllers)
        {
            var name = controller.Name.EndsWith("Controller") ? controller.Name[..^"Controller".Length] : controller.Name;
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);
            foreach (var action in actions)
            {
                var verbs = action.GetCustomAttributes<HttpMethodAttribute>()
                    .SelectMany(a => a.HttpMethods).Distinct().OrderBy(v => v).ToList();
                var verb = verbs.Count == 0 ? "ANY" : string.Join("/", verbs);
                lines.Add($"{name}.{action.Name} {verb}: {Access(controller, action)}");
            }
        }
        return lines.OrderBy(l => l, StringComparer.Ordinal).ToList();
    }

    private static string Access(Type controller, MethodInfo action)
    {
        if (controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
            || action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any())
            return "anyone";

        var rules = controller.GetCustomAttributes(true).OfType<IAuthorizeData>()
            .Concat(action.GetCustomAttributes(true).OfType<IAuthorizeData>())
            .ToList();
        if (rules.Count == 0) return "anyone";
        if (rules.All(r => string.IsNullOrEmpty(r.Policy) && string.IsNullOrEmpty(r.Roles))) return "signed-in";

        IEnumerable<string> allowed = Roles;
        foreach (var rule in rules)
        {
            if (!string.IsNullOrEmpty(rule.Policy))
                allowed = allowed.Intersect(RolesFor(rule.Policy));
            if (!string.IsNullOrEmpty(rule.Roles))
                allowed = allowed.Intersect(rule.Roles.Split(',').Select(r => r.Trim()));
        }

        var list = allowed.ToList();
        return list.Count == 0 ? "nobody" : string.Join(" ", list);
    }

    private static IEnumerable<string> RolesFor(string policy)
    {
        if (policy.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            var code = policy[PermissionPolicy.Prefix.Length..];
            return SystemGroups.All.Where(g => g.Permissions.Contains(code)).Select(g => g.RoleName);
        }

        Assert.True(LegacyRolePolicies.All.ContainsKey(policy), $"Unknown policy '{policy}'.");
        return LegacyRolePolicies.All[policy];
    }

    private static string BaselinePath([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "access-matrix.txt");

    [Fact]
    public void Access_matches_the_approved_baseline()
    {
        var path = BaselinePath();
        var actual = string.Join("\n", Compute()) + "\n";

        if (!File.Exists(path))
        {
            File.WriteAllText(path, actual);
            Assert.Fail($"No baseline yet. It has been written to {path}: review it and commit it.");
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected != actual)
        {
            File.WriteAllText(Path.ChangeExtension(path, ".actual.txt"), actual);
            var changed = actual.Split('\n').Except(expected.Split('\n'));
            Assert.Fail("Access changed. New or different lines (full list in access-matrix.actual.txt):\n"
                        + string.Join("\n", changed));
        }
    }

    [Fact]
    public void Every_required_permission_is_in_the_catalogue()
    {
        var known = PermissionCatalogue.All.Select(p => p.Code).ToHashSet();
        var used = Controllers.SelectMany(c => c.GetCustomAttributes<RequirePermissionAttribute>(true)
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<RequirePermissionAttribute>(true))))
            .Select(a => a.Code);

        Assert.All(used, code => Assert.Contains(code, known));
    }
}