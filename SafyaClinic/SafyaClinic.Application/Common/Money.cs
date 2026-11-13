using System.Globalization;

namespace SafyaClinic.Application.Common;

/// <summary>Money display (F2, D10): "1,250.50 EGP", Western digits in every UI language.</summary>
public static class Money
{
    public const string Currency = "EGP";

    public static string Format(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture) + " " + Currency;

    public static string ToMoney(this decimal amount) => Format(amount);

    /// <summary>To whole piastres (two decimals), half away from zero, so it fits the money fields.</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}