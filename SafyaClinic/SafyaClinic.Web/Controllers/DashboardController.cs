using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Models;

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
            ? todayResult.Data?.ToList() ?? []
            : [];
        var today = DateTime.Today;
        var endOfToday = today.AddDays(1).AddSeconds(-1);

        var todayPayments = await _paymentService.GetRevenueByDateRangeAsync(
            today,
            endOfToday);

        var model = new DashboardViewModel
        {
            TodayReservations = reservations,
            TodayReservationCount = reservations.Count,
            TodayPendingCount = reservations.Count(r => r.StatusName == "Pending"),
            TodayUnpaidCount = reservations.Count(r => !r.IsPaid),
            TodayRevenue = todayPayments.IsSuccess
                ? todayPayments.Data
                : 0m
        };

        return View(model);
    }
}