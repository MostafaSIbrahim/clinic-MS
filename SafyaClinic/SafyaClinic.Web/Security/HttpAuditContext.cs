using System.Security.Claims;
using SafyaClinic.Application.Common.Auditing;

namespace SafyaClinic.Web.Security;

/// <summary>Audit context from the current request: signed-in user, client IP, request id (F3b).</summary>
public sealed class HttpAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpAuditContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;

    public int? UserId =>
        int.TryParse(Http?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();

    public string? RequestId => Http?.TraceIdentifier;
}