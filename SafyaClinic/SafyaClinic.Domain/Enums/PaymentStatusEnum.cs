namespace SafyaClinic.Domain.Enums
{
    public enum PaymentStatusEnum
    {
        Active,
        /// <summary>Voided: as if never collected. Doesn't cover the visit (P6).</summary>
        Cancelled,
        /// <summary>P6: an excused balance. Covers the visit but is never revenue.</summary>
        WrittenOff
    }
}
