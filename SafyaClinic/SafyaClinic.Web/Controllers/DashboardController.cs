using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IReservationService _reservationService;
 
    private readonly IPaymentService _paymentService;

    public DashboardController(
        IReservationService reservationService,
        IPaymentService paymentService)
    {
        _reservationService = reservationService;  
        _paymentService = paymentService;
    }

    public async Task<IActionResult> Index()
    {
        var doctorId = (IsDoctor || IsNutritionist)
            ? CurrentUserId
            : (int?)null;

        var todayResult = await _reservationService.GetTodayReservationsAsync(doctorId);

        var reservations = todayResult.IsSuccess
            ? todayResult.Data?.ToList() ?? new List<ReservationSummaryDto>()
            : new List<ReservationSummaryDto>();

        ViewBag.TodayReservations = reservations;
        ViewBag.TodayReservationCount = reservations.Count;
        ViewBag.TodayPendingCount = reservations.Count(r => r.StatusName == "Pending");
        ViewBag.TodayUnpaidCount = reservations.Count(r => !r.IsPaid);

        var todayPayments = await _paymentService.GetRevenueByDateRangeAsync(
            DateTime.Today,
            DateTime.Today.AddDays(1).AddSeconds(-1));

        ViewBag.TodayRevenue = todayPayments.IsSuccess
            ? todayPayments.Data
            : 0m;

        return View();
    }
}