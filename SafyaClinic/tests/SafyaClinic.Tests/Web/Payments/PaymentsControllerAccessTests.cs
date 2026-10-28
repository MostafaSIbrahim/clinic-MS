using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Controllers;
using Xunit;

namespace SafyaClinic.Tests.Web.Payments;

/// <summary>
/// Ported from tests/PaymentAccessChecks (30 console checks, 2026-09).
/// Server-derived report scopes and action authorization for PaymentsController.
/// </summary>
public class PaymentsControllerAccessTests
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    private static (PaymentsController Controller, PaymentServiceSpy Spy) Create(params string[] roles)
    {
        var service = DispatchProxy.Create<IPaymentService, PaymentServiceSpy>();
        var claims = roles
            .Select(role => new Claim(ClaimTypes.Role, role))
            .Append(new Claim(ClaimTypes.NameIdentifier, "42"));

        var controller = new PaymentsController(service, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "tests"))
                }
            }
        };

        return (controller, (PaymentServiceSpy)(object)service);
    }

    [Theory]
    [InlineData("Reception")]
    [InlineData("ReceptionAdmin")]
    [InlineData("Admin")]
    [InlineData("Doctor")]
    public async Task Report_without_dates_does_not_query(string role)
    {
        var (controller, spy) = Create(role);

        await controller.Report(null, null);

        Assert.Empty(spy.Calls);
    }

    [Theory]
    [InlineData("Reception")]
    [InlineData("ReceptionAdmin")]
    public async Task Front_desk_report_is_restricted_to_today(string role)
    {
        var (controller, spy) = Create(role);

        await controller.Report(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31));

        var args = Assert.Single(spy.Calls).Args;
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Cairo).Date;
        Assert.Equal(today, TimeZoneInfo.ConvertTimeFromUtc((DateTime)args[0]!, Cairo).Date);
        Assert.Equal(today.AddDays(1), TimeZoneInfo.ConvertTimeFromUtc((DateTime)args[1]!, Cairo).Date);
    }

    [Theory]
    [InlineData("Reception")]
    [InlineData("ReceptionAdmin")]
    public async Task Front_desk_dashboard_is_unpaid_only(string role)
    {
        var (controller, spy) = Create(role);

        var view = Assert.IsType<ViewResult>(await controller.Dashboard(null, null));

        Assert.Equal("Unpaid", view.ViewName);
        Assert.True((bool)spy.Calls.Last().Args[2]!);
    }

    [Theory]
    [InlineData("Admin", null)]
    [InlineData("Doctor", 42)]
    public async Task Report_is_scoped_by_role(string role, int? expectedDoctorId)
    {
        var (controller, spy) = Create(role);

        await controller.Report(new DateTime(2026, 9, 2), new DateTime(2026, 9, 3));

        var args = Assert.Single(spy.Calls).Args;
        Assert.Equal(expectedDoctorId, (int?)args[2]);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Doctor")]
    public async Task Report_includes_the_entire_final_date(string role)
    {
        var (controller, spy) = Create(role);

        await controller.Report(new DateTime(2026, 9, 2), new DateTime(2026, 9, 3));

        var args = Assert.Single(spy.Calls).Args;
        Assert.Equal(new DateTime(2026, 9, 4), TimeZoneInfo.ConvertTimeFromUtc((DateTime)args[1]!, Cairo));
    }

    [Fact]
    public async Task Admin_role_takes_precedence_in_report()
    {
        var (controller, spy) = Create("Admin", "Doctor", "Reception");

        await controller.Report(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));

        Assert.Null(Assert.Single(spy.Calls).Args[2]);
    }

    [Fact]
    public async Task Admin_gets_full_dashboard()
    {
        var (controller, spy) = Create("Admin", "Doctor", "Reception");

        var view = Assert.IsType<ViewResult>(await controller.Dashboard(null, null));

        Assert.Equal("Dashboard", view.ViewName);
        Assert.False((bool)spy.Calls.Last().Args[2]!);
    }

    public static TheoryData<DateTime, DateTime> InvalidRanges => new()
    {
        { new DateTime(2026, 9, 3), new DateTime(2026, 9, 2) },
        { DateTime.MaxValue.Date, DateTime.MaxValue.Date }
    };

    [Theory]
    [MemberData(nameof(InvalidRanges))]
    public async Task Invalid_range_does_not_query(DateTime from, DateTime to)
    {
        var (controller, spy) = Create("Doctor");

        await controller.Report(from, to);

        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(spy.Calls);
    }

    [Fact]
    public async Task Cairo_spring_DST_day_covers_23_real_hours()
    {
        var (controller, spy) = Create("Doctor");

        await controller.Report(new DateTime(2026, 4, 24), new DateTime(2026, 4, 24));

        var args = Assert.Single(spy.Calls).Args;
        Assert.Equal(TimeSpan.FromHours(23), (DateTime)args[1]! - (DateTime)args[0]!);
    }

    [Fact]
    public void Every_action_declares_its_expected_payment_policy()
    {
        var actions = typeof(PaymentsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();

        var missing = actions
            .Where(action => !action.GetCustomAttributes<AuthorizeAttribute>()
                .Any(attribute => attribute.Policy == ExpectedPolicy(action.Name)))
            .Select(action => $"{action.Name} (expected {ExpectedPolicy(action.Name)})")
            .ToList();

        Assert.NotEmpty(actions);
        Assert.Empty(missing);
    }

    private static string ExpectedPolicy(string action) => action switch
    {
        "Report" => "PaymentReports",
        "DashboardLineDetails" or "RecalculateAllPaidStatuses" or "BackfillZeroCostPayments" => "AdminOnly",
        _ => "PaymentStaff"
    };
}