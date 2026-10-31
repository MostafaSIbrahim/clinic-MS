using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Tests.Web.Payments;
using SafyaClinic.Web.Controllers;
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
        var controller = new DashboardController(reservations, payments, clock)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        await controller.Index();

        var call = ((PaymentServiceSpy)(object)payments).Calls.Single(c => c.Name == "GetRevenueBetweenAsync");
        Assert.Equal(new DateTime(2026, 12, 1, 22, 0, 0), (DateTime)call.Args[0]!);
        Assert.Equal(new DateTime(2026, 12, 2, 22, 0, 0), (DateTime)call.Args[1]!);
    }
}