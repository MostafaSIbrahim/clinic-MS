using System.Text;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Web.Reports;
using Xunit;

namespace SafyaClinic.Tests.Web.Payments;

/// <summary>M2d: the payments report as a CSV file for Excel.</summary>
public class PaymentReportCsvTests
{
    private static readonly IClinicClock Cairo = ClinicClock.Cairo;

    private static string[] Lines(byte[] csv)
    {
        Assert.Equal(Encoding.UTF8.GetPreamble(), csv.Take(3));   // Excel needs the byte-order mark for Arabic
        return Encoding.UTF8.GetString(csv, 3, csv.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void Each_payment_is_one_line_in_clinic_time_with_plain_amounts()
    {
        var csv = PaymentReportCsv.Write(new[]
        {
            new PaymentDto
            {
                PaymentDate = new DateTime(2026, 11, 12, 22, 30, 0),   // UTC: 00:30 on 13 Nov in Cairo
                PatientName = "منى عادل", Amount = 1250.5m, PaymentMethod = "CreditCard", ClinicName = "Main",
                DoctorName = "Dr Karim", Status = "Active", CollectorName = "Desk"
            },
            new PaymentDto
            {
                PaymentDate = new DateTime(2026, 11, 12, 8, 0, 0), PatientName = "Mona", Amount = 300m,
                PaymentMethod = "WriteOff", Status = "WrittenOff", WriteOffReason = "StaffOrFamily", CollectorName = "Sup"
            },
        }, Cairo);

        var lines = Lines(csv);
        Assert.Equal(string.Join(",", PaymentReportCsv.Header), lines[0]);
        Assert.Equal("2026-11-13 00:30,منى عادل,1250.50,Credit Card,Main,,Dr Karim,Active,,Desk,", lines[1]);
        Assert.Equal("2026-11-12 10:00,Mona,300.00,Write Off,,,,Written off,Staff Or Family,Sup,", lines[2]);
    }

    [Theory]
    [InlineData("Smith, John", "\"Smith, John\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(1)", "'=HYPERLINK(1)")]   // never a formula in Excel
    [InlineData("-5", "-5")]                           // a plain number stays a number
    [InlineData(null, "")]
    public void Values_are_quoted_and_never_read_as_formulas(string? value, string expected)
    {
        Assert.Equal(expected, PaymentReportCsv.Field(value));
    }
}
