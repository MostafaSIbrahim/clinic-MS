using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Reservation
{
    public class ReservationStatus: BaseEntity
    {
        public string StatusName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ColorCode { get; set; } = "#6c757d";
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
