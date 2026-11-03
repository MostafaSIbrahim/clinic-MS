using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SafyaClinic.Web.Security;

/// <summary>F4b: a permission's policy name is "perm:" followed by the permission code.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string For(string code) => Prefix + code;
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string code) => Code = code;

    public string Code { get; }
}

/// <summary>
/// F4b: <c>[RequirePermission(Permissions.Records.Lock)]</c> on a controller or action.
/// Like [Authorize], every attribute on the controller and the action must be met.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string code) : base(PermissionPolicy.For(code)) => Code = code;

    public string Code { get; }
}

/// <summary>F4b: builds "perm:" policies on demand; every other policy name is looked up as before.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicy.Prefix.Length..]))
            .Build();
    }
}

/// <summary>F4b: met when the signed-in user's groups grant the permission.</summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentPermissions _permissions;

    public PermissionHandler(ICurrentPermissions permissions) => _permissions = permissions;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await _permissions.HasAsync(requirement.Code))
            context.Succeed(requirement);
    }
}