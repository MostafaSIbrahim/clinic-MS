using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Identity
{
    public class User : AuditableEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Specialization { get; set; }          // For doctors: Internal/Nutrition/Both
        public string? LicenseNumber { get; set; }          // Medical license number
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public virtual ICollection<Patient> CreatedPatients { get; set; } = new List<Patient>();
        public virtual ICollection<Reservation> DoctorReservations { get; set; } = new List<Reservation>();
    }
}
