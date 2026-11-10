using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Tests.Web.Payments;
using SafyaClinic.Tests.Web.Security;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F2 / F14: today's revenue is the Cairo day, as a UTC range.</summary>
public class DashboardControllerTests
{
    [Fact]
    public async Task Today_revenue_counts_payments_from_cairo_midnight()
    {
        var payments = DispatchProxy.Create<IPaymentService, PaymentServiceSpy>();
        var reservations = DispatchProxy.Create<IReservationService, ReservationServiceStub>();
        // 22:30 UTC on 1 Dec is 00:30 on 2 Dec in Cairo: "today" must be 2 Dec.
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId),
            () => new DateTime(2026, 12, 1, 22, 30, 0, DateTimeKind.Utc));
        var controller = Controller(reservations, payments, clock, SystemGroups.ReceptionRole);

        await controller.Index();

        var call = ((PaymentServiceSpy)(object)payments).Calls.Single(c => c.Name == "GetRevenueBetweenAsync");
        Assert.Equal(new DateTime(2026, 12, 1, 22, 0, 0), (DateTime)call.Args[0]!);
        Assert.Equal(new DateTime(2026, 12, 2, 22, 0, 0), (DateTime)call.Args[1]!);
    }

    // P8a: money figures only for people who may see payments; the list only for people who may see reservations.
    [Theory]
    [InlineData(SystemGroups.ReceptionRole, true, true)]
    [InlineData(SystemGroups.ReceptionAdminRole, true, true)]
    [InlineData(SystemGroups.DoctorRole, true, false)]
    [InlineData(null, false, false)]
    public async Task Dashboard_shows_what_the_user_may_see(string? role, bool reservations, bool money)
    {
        var payments = DispatchProxy.Create<IPaymentService, PaymentServiceSpy>();
        var controller = Controller(DispatchProxy.Create<IReservationService, ReservationServiceStub>(), payments,
            new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => DateTime.UtcNow), role);

        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);

        Assert.Equal(reservations, model.CanSeeReservations);
        Assert.Equal(money, model.CanSeeMoney);
        Assert.Equal(money, ((PaymentServiceSpy)(object)payments).Calls.Any(c => c.Name == "GetRevenueBetweenAsync"));
    }

    private static DashboardController Controller(IReservationService reservations, IPaymentService payments,
        IClinicClock clock, string? role) =>
        new(reservations, payments, clock, new RolePermissions(role is null ? Array.Empty<string>() : new[] { role }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };
}