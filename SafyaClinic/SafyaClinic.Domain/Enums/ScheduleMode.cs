namespace SafyaClinic.Domain.Enums;

/// <summary>P1: how a doctor's calendar works in a clinic (D7).</summary>
public enum ScheduleMode
{
    /// <summary>Bookable at any step inside the doctor's working periods, except breaks.</summary>
    Open = 1,

    /// <summary>Bookable only inside the slots the doctor set.</summary>
    SpecificSlots = 2,
}

/// <summary>P1: what a period in a doctor's weekly pattern is.</summary>
public enum ScheduleTimeKind
{
    Working = 1,
    Break = 2,
    Slot = 3,
}
