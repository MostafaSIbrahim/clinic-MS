using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IReservationService _reservationService;
 
    private readonly IPaymentService _paymentService;

    public DashboardController(
        IReservationService reservationService,
        IPatientService patientService,
        IPaymentService paymentService)
    {
        _reservationService = reservationService;
     
        _paymentService = paymentService;
    }

    public async Task<IActionResult> Index()
    {
        var doctorId = (IsDoctor || IsNutritionist) ? CurrentUserId : (int?)null;

        var todayResult = await _reservationService.GetTodayReservationsAsync(doctorId);
        var todayPayments = await _paymentService.GetRevenueByDateRangeAsync(
            DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

        ViewBag.TodayReservations = todayResult.IsSuccess ? todayResult.Data : Enumerable.Empty<object>();
        ViewBag.TodayRevenue = todayPayments.IsSuccess
            ? todayPayments.Data! : 0m;

        return View();
    }
}