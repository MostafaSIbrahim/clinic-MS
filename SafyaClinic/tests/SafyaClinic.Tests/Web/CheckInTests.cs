using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Controllers;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>P4: checking in from the Appointments day returns there, never to another site.</summary>
public class CheckInTests
{
    public class CheckInSucceeds : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IReservationService.CheckInAsync) => Task.FromResult(ServiceResult.Success("Checked in.")),
            _ => throw new NotSupportedException(method.Name),
        };
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static ReservationsController Controller()
    {
        var http = new DefaultHttpContext();
        var context = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
        return new ReservationsController(DispatchProxy.Create<IReservationService, CheckInSucceeds>(), null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext(context),
            TempData = new TempDataDictionary(http, new NoTempData()),
            Url = new UrlHelper(context),
        };
    }

    [Fact]
    public async Task Check_in_from_appointments_returns_to_the_same_day()
    {
        var result = await Controller().CheckIn(41, "/Appointments?date=2026-11-07&clinicId=1");

        Assert.Equal("/Appointments?date=2026-11-07&clinicId=1", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example")]
    public async Task Without_a_local_return_address_it_opens_the_reservation(string? returnUrl)
    {
        var result = await Controller().CheckIn(41, returnUrl);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(41, redirect.RouteValues!["id"]);
    }
}
