using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Patient
{
    public class PatientPhone : BaseEntity
    {
        public int PatientId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string PhoneType { get; set; } = "Mobile";   // Mobile, Home, Work, Emergency
        public bool IsPrimary { get; set; }

        // Navigation properties
        public virtual Patient Patient { get; set; } = null!;
    }
}
