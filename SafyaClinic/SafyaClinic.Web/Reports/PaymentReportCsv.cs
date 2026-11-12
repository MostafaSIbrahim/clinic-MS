using System.Globalization;
using System.Text;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Payment;

namespace SafyaClinic.Web.Reports;

/// <summary>
/// M2d: the payments report as CSV for Excel: UTF-8 with a byte-order mark (so Arabic names show), comma
/// separated, times in clinic time, amounts with a dot and two decimals. A value that a spreadsheet would read as
/// a formula (starting with = + - @) is prefixed with an apostrophe.
/// </summary>
public static class PaymentReportCsv
{
    public static readonly string[] Header =
    {
        "Date", "Patient", "Amount", "Method", "Clinic", "Source", "Doctor", "Status", "Write-off reason", "Collected by", "Reference"
    };

    public static byte[] Write(IEnumerable<PaymentDto> payments, IClinicClock clock)
    {
        var text = new StringBuilder();
        Line(text, Header);
        foreach (var p in payments)
        {
            Line(text, new[]
            {
                clock.ToClinicTime(DateTime.SpecifyKind(p.PaymentDate, DateTimeKind.Utc)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                p.PatientName,
                p.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                Labels.Humanize(p.PaymentMethod),
                p.ClinicName ?? "",
                p.PatientSourceName ?? "",
                p.DoctorName ?? "",
                p.Status switch { "WrittenOff" => "Written off", "Cancelled" => "Cancelled (void)", _ => "Active" },
                p.WriteOffReason is null ? "" : Labels.Humanize(p.WriteOffReason),
                p.CollectorName,
                p.ReferenceNumber ?? ""
            });
        }
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(text.ToString());
        return preamble.Concat(body).ToArray();
    }

    private static void Line(StringBuilder text, IEnumerable<string> values) =>
        text.Append(string.Join(",", values.Select(Field))).Append("\r\n");

    public static string Field(string? value)
    {
        value ??= "";
        if (value.Length > 0 && "=+-@".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
