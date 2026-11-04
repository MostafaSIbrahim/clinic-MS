using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Settings;

/// <summary>
/// P1: one opening period of a clinic on a weekday, in clinic (Cairo) time. A day can have several
/// periods (for example 10:00–14:00 and 17:00–21:00); a day with no periods is closed.
/// </summary>
public class ClinicOpeningHours : BaseEntity
{
    public int ClinicId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan OpensAt { get; set; }
    public TimeSpan ClosesAt { get; set; }

    public virtual Clinic Clinic { get; set; } = null!;
}
