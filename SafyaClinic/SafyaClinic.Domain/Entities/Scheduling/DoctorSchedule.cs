using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Domain.Entities.Scheduling;

/// <summary>
/// P1: a doctor's calendar in one clinic: the mode, the booking step and a weekly pattern.
/// One per doctor and clinic. Times are clinic (Cairo) time.
/// </summary>
public class DoctorSchedule : BaseEntity
{
    public int DoctorId { get; set; }
    public int ClinicId { get; set; }
    public ScheduleMode Mode { get; set; } = ScheduleMode.Open;
    public int StepMinutes { get; set; } = 15;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
    public int? UpdatedBy { get; set; }

    public virtual User Doctor { get; set; } = null!;
    public virtual Clinic Clinic { get; set; } = null!;
    public virtual ICollection<DoctorScheduleTime> Times { get; set; } = new List<DoctorScheduleTime>();
    public virtual ICollection<DoctorScheduleChange> Changes { get; set; } = new List<DoctorScheduleChange>();
}

/// <summary>
/// P1c: a date that differs from the weekly pattern. The rows of one date replace that weekday's pattern:
/// either one day-off row, or that date's own working periods and breaks (open) or slots (specific slots).
/// </summary>
public class DoctorScheduleChange : BaseEntity
{
    public int ScheduleId { get; set; }
    public DateTime Date { get; set; }
    public bool IsDayOff { get; set; }
    public ScheduleTimeKind? Kind { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    public virtual DoctorSchedule Schedule { get; set; } = null!;
}

/// <summary>P1: one period of a doctor's weekly pattern: a working period, a break or a slot.</summary>
public class DoctorScheduleTime : BaseEntity
{
    public int ScheduleId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public ScheduleTimeKind Kind { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public virtual DoctorSchedule Schedule { get; set; } = null!;
}
