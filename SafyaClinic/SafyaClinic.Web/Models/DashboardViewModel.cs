using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Web.Models;

public class DashboardViewModel
{
    public IEnumerable<ReservationSummaryDto> TodayReservations { get; set; }
        = Enumerable.Empty<ReservationSummaryDto>();

    public int TodayReservationCount { get; set; }

    public int TodayPendingCount { get; set; }

    public int TodayUnpaidCount { get; set; }

    public decimal TodayRevenue { get; set; }
}