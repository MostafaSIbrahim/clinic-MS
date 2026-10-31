using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Models;
using SafyaClinic.Application.Common;

namespace SafyaClinic.Web.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IClinicClock _clock;

    public DashboardController(
        IReservationService reservationService,
        IPaymentService paymentService,
        IClinicClock clock)
    {
        _reservationService = reservationService;
        _paymentService = paymentService;
        _clock = clock;
    }

    public async Task<IActionResult> Index()
    {
        var doctorId = (IsDoctor || IsNutritionist)
            ? CurrentUserId
            : (int?)null;

        var todayResult = await _reservationService.GetTodayReservationsAsync(doctorId);

        var reservations = todayResult.IsSuccess
            ? todayResult.Data?.ToList() ?? []
            : [];
        // F2: the clinic's day, as an exact UTC range (payments are stored in UTC).
        var today = _clock.Today;
        var todayPayments = await _paymentService.GetRevenueBetweenAsync(
            _clock.DayStartUtc(today),
            _clock.DayStartUtc(today.AddDays(1)));

        var model = new DashboardViewModel
        {
            TodayReservations = reservations,
            TodayReservationCount = reservations.Count,
            TodayPendingCount = reservations.Count(r => r.StatusName == "Pending"),
            TodayUnpaidCount = reservations.Count(r => r.HasCollectibleBalance),
            TodayRevenue = todayPayments.IsSuccess
                ? todayPayments.Data
                : 0m
        };

        return View(model);
    }
}