using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Controllers;

// Payment access: database-free regression checks for server-derived report scopes and action guards.
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
(PaymentsController Controller, PaymentSpy Spy) Create(params string[] roles)
{
    var service = DispatchProxy.Create<IPaymentService, PaymentSpy>();
    var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.NameIdentifier, "42"));
    var controller = new PaymentsController(service, null!, null!) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "tests")) } }
    };
    return (controller, (PaymentSpy)(object)service);
}
var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
foreach (var role in new[] { "Reception", "ReceptionAdmin" })
{
    var (controller, spy) = Create(role);
    await controller.Report(null, null);
    Check(spy.Calls.Count == 0, role + " initial report does not query");
    await controller.Report(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31));
    var callArgs = spy.Calls.Single().Args;
    var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
    Check(TimeZoneInfo.ConvertTimeFromUtc((DateTime)callArgs[0]!, zone).Date == today &&
          TimeZoneInfo.ConvertTimeFromUtc((DateTime)callArgs[1]!, zone).Date == today.AddDays(1), role + " tampered range restricted to today");
    var dashboard = (ViewResult)await controller.Dashboard(null, null);
    Check(dashboard.ViewName == "Unpaid" && (bool)spy.Calls.Last().Args[2]!, role + " receives unpaid-only query/view");
}
foreach (var role in new[] { "Admin", "Doctor" })
{
    var (controller, spy) = Create(role);
    await controller.Report(null, null);
    Check(spy.Calls.Count == 0, role + " initial report does not query");
    await controller.Report(new DateTime(2026, 9, 2), new DateTime(2026, 9, 3));
    var callArgs = spy.Calls.Single().Args;
    Check((int?)callArgs[2] == (role == "Doctor" ? 42 : null), role + " correct doctor scope");
    Check(TimeZoneInfo.ConvertTimeFromUtc((DateTime)callArgs[1]!, zone) == new DateTime(2026, 9, 4), role + " includes entire final date");
}
{
    var (controller, spy) = Create("Admin", "Doctor", "Reception");
    await controller.Report(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));
    Check(spy.Calls.Single().Args[2] == null, "Admin role takes precedence");
    var view = (ViewResult)await controller.Dashboard(null, null);
    Check(view.ViewName == "Dashboard" && !(bool)spy.Calls.Last().Args[2]!, "Admin full dashboard");
}
foreach (var dates in new[] {
    (new DateTime(2026, 9, 3), new DateTime(2026, 9, 2)),
    (DateTime.MaxValue.Date, DateTime.MaxValue.Date) })
{
    var (controller, spy) = Create("Doctor");
    await controller.Report(dates.Item1, dates.Item2);
    Check(!controller.ModelState.IsValid && spy.Calls.Count == 0, "Invalid range does not query");
}
{
    var (controller, spy) = Create("Doctor");
    await controller.Report(new DateTime(2026, 4, 24), new DateTime(2026, 4, 24));
    var callArgs = spy.Calls.Single().Args;
    Check((DateTime)callArgs[1]! - (DateTime)callArgs[0]! == TimeSpan.FromHours(23), "Cairo spring DST day covers 23 real hours");
}
foreach (var method in typeof(PaymentsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
{
    var expected = method.Name switch {
        "Report" => "PaymentReports",
        "DashboardLineDetails" or "RecalculateAllPaidStatuses" or "BackfillZeroCostPayments" => "AdminOnly",
        _ => "PaymentStaff"
    };
    Check(method.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy == expected), method.Name + " action authorization");
}
Console.WriteLine($"{checks} checks passed. No database used.");

public class PaymentSpy : DispatchProxy
{
    public List<(string Name, object?[] Args)> Calls { get; } = new();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls.Add((targetMethod!.Name, args!));
        return targetMethod.Name switch {
            "GetPaymentsByDateRangeAsync" => Task.FromResult(ServiceResult<IEnumerable<PaymentDto>>.Success(Array.Empty<PaymentDto>())),
            "GetPaymentDashboardAsync" => Task.FromResult(ServiceResult<PaymentDashboardDto>.Success(new PaymentDashboardDto())),
            _ => throw new NotSupportedException(targetMethod.Name)
        };
    }
}
