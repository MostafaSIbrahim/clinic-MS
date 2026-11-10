using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Models;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

[Authorize]
public class DashboardController : BaseController
{
    private readonly IReservationService _reservationService;
    private readonly IPaymentService _paymentService;
    private readonly IClinicClock _clock;
    private readonly ICurrentPermissions _can;

    public DashboardController(
        IReservationService reservationService,
        IPaymentService paymentService,
        IClinicClock clock,
        ICurrentPermissions can)
    {
        _reservationService = reservationService;
        _paymentService = paymentService;
        _clock = clock;
        _can = can;
    }

    public async Task<IActionResult> Index()
    {
        // P8a: today's list is for people who may see reservations; the money figures for people who
        // may see payments. Clinicians see their own day (F4c).
        var canSeeReservations = await _can.HasAsync(Permissions.Reservations.View);
        var canSeeMoney = await _can.HasAsync(Permissions.Payments.View);

        var reservations = new List<ReservationSummaryDto>();
        if (canSeeReservations)
        {
            var doctorId = IsClinician ? CurrentUserId : (int?)null;
            var todayResult = await _reservationService.GetTodayReservationsAsync(doctorId);
            if (todayResult.IsSuccess && todayResult.Data is not null)
                reservations = todayResult.Data.ToList();
        }

        var revenue = 0m;
        if (canSeeMoney)
        {
            // F2: the clinic's day, as an exact UTC range (payments are stored in UTC).
            var today = _clock.Today;
            var todayPayments = await _paymentService.GetRevenueBetweenAsync(
                _clock.DayStartUtc(today),
                _clock.DayStartUtc(today.AddDays(1)));
            if (todayPayments.IsSuccess) revenue = todayPayments.Data;
        }

        var model = new DashboardViewModel
        {
            TodayReservations = reservations,
            TodayReservationCount = reservations.Count,
            TodayPendingCount = reservations.Count(r => r.StatusName == "Pending"),
            TodayUnpaidCount = reservations.Count(r => r.HasCollectibleBalance),
            TodayRevenue = revenue,
            CanSeeReservations = canSeeReservations,
            CanSeeMoney = canSeeMoney
        };

        return View(model);
    }
}