namespace SafyaClinic.Domain.Enums
{
    /// <summary>
    /// F4a: who appears in doctor pickers and queues. Replaces the role-ID checks
    /// (2 = Doctor, 5 = Nutritionist). Stored as text.
    /// </summary>
    public enum ClinicianType
    {
        None = 0,
        Doctor = 1,
        Nutritionist = 2
    }
}