namespace SafyaClinic.Domain.Enums;

/// <summary>P6b: why a balance was written off. "Other" needs a note.</summary>
public enum WriteOffReason
{
    Discount,
    Hardship,
    StaffOrFamily,
    BillingError,
    Other
}
