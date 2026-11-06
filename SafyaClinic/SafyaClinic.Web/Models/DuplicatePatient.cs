using System.Text.RegularExpressions;

namespace SafyaClinic.Web.Models;

/// <summary>P3: the existing patient named in a "this phone already belongs to … (patient #12)" refusal.</summary>
public static class DuplicatePatient
{
    private static readonly Regex PatientNumber = new(@"\(patient #(\d+)\)", RegexOptions.CultureInvariant);

    public static int? FindId(IEnumerable<string> errors) =>
        errors.Select(e => PatientNumber.Match(e)).Where(m => m.Success).Select(m => (int?)int.Parse(m.Groups[1].Value)).FirstOrDefault();
}
