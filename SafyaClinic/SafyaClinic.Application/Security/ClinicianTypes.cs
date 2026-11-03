using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Security;

/// <summary>F4a: until roles are retired (F4d) the clinician type follows the roles; Doctor wins over Nutritionist.</summary>
public static class ClinicianTypes
{
    public static ClinicianType FromRoles(IEnumerable<string> roleNames)
    {
        var names = roleNames.ToList();
        if (names.Contains(SystemGroups.DoctorRole)) return ClinicianType.Doctor;
        if (names.Contains(SystemGroups.NutritionistRole)) return ClinicianType.Nutritionist;
        return ClinicianType.None;
    }
}