using System.Text;

namespace SafyaClinic.Application.Common;

/// <summary>
/// One stored form for phone numbers, so the same number always matches (F5b, D8):
/// digits only (Arabic-Indic digits become 0–9) and Egypt's international prefix becomes
/// the local 0. "+20 100 123 4567", "0020-100-1234567" and "٠١٠٠١٢٣٤٥٦٧" all become "01001234567".
/// </summary>
public static class PhoneNumbers
{
    public const int MinimumDigits = 7;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var digits = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9') digits.Append(c);
            else if (c is >= '\u0660' and <= '\u0669') digits.Append((char)('0' + (c - '\u0660')));   // Arabic-Indic
            else if (c is >= '\u06F0' and <= '\u06F9') digits.Append((char)('0' + (c - '\u06F0')));   // Extended Arabic-Indic
        }

        var number = digits.ToString();
        if (number.StartsWith("0020")) return "0" + number[4..];
        if (number.StartsWith("20") && number.Length >= 11) return "0" + number[2..];
        return number;
    }
}