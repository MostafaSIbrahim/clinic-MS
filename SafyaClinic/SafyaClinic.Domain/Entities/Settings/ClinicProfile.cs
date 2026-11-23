using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Settings
{
    /// <summary>
    /// M5d: the clinic's details as printed on prescriptions and lab slips, in English and Arabic.
    /// One row at most; until it is saved, the printouts use the ClinicInfo server setting.
    /// </summary>
    public class ClinicProfile : AuditableEntity
    {
        public string NameEn { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        public string? TaglineEn { get; set; }
        public string? TaglineAr { get; set; }
        public string? AddressEn { get; set; }
        public string? AddressAr { get; set; }
        public string? Phone { get; set; }
        public string? SecondPhone { get; set; }
        public string? Email { get; set; }
        public string? LicenceLine { get; set; }   // e.g. a licence or registration number, printed at the foot
    }
}
