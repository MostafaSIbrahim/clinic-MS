using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Security;

/// <summary>F4b: the signed-in user's permissions for the current request.</summary>
public interface ICurrentPermissions
{
    Task<bool> HasAsync(string code);
}

/// <summary>
/// Read at most once per request and cached for <see cref="CacheDuration"/> per user and
/// security stamp: a role change (which renews the stamp) applies at once, a group change
/// (F4d) within a minute.
/// </summary>
public sealed class CurrentPermissions : ICurrentPermissions
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    private readonly IHttpContextAccessor _http;
    private readonly IPermissionStore _store;
    private readonly IMemoryCache _cache;
    private IReadOnlySet<string>? _loaded;

    public CurrentPermissions(IHttpContextAccessor http, IPermissionStore store, IMemoryCache cache)
    {
        _http = http;
        _store = store;
        _cache = cache;
    }

    public async Task<bool> HasAsync(string code) => (await LoadAsync()).Contains(code);

    private async Task<IReadOnlySet<string>> LoadAsync()
    {
        if (_loaded is not null) return _loaded;

        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true
            || !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return _loaded = None;
        }

        var key = $"permissions:{userId}:{user.FindFirstValue(AuthClaims.SecurityStamp)}";
        _loaded = await _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return _store.GetPermissionsAsync(userId);
        }) ?? None;
        return _loaded;
    }
}