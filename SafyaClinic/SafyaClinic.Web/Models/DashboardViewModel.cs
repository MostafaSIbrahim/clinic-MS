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

    // P8a: the list and the money figures are shown only to people allowed to see them.
    public bool CanSeeReservations { get; set; }
    public bool CanSeeMoney { get; set; }
}