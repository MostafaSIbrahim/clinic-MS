using System.Linq.Expressions;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Billing;

/// <summary>
/// P6: the one rule for what a visit's payments cover. Every page that shows "Covered", a balance due or the
/// stored <c>Reservations.IsPaid</c> uses this, so they always agree.
/// <list type="bullet">
/// <item><b>Active</b> payments are money collected: they cover the visit and are revenue.</item>
/// <item><b>WrittenOff</b> payments are an excused balance: they cover the visit but are never revenue.</item>
/// <item><b>Cancelled</b> payments are voided: as if never collected, so the balance reopens.</item>
/// </list>
/// </summary>
public static class Coverage
{
    /// <summary>For database queries: <c>r.Payments.AsQueryable().Where(Coverage.Covers)</c>.</summary>
    public static readonly Expression<Func<Payment, bool>> Covers =
        p => p.Status == PaymentStatusEnum.Active || p.Status == PaymentStatusEnum.WrittenOff;

    public static bool CoversStatus(PaymentStatusEnum status) =>
        status is PaymentStatusEnum.Active or PaymentStatusEnum.WrittenOff;

    /// <summary>For DTOs that carry the status as text ("Active", "WrittenOff", "Cancelled").</summary>
    public static bool CoversStatus(string? status) =>
        Enum.TryParse<PaymentStatusEnum>(status, out var parsed) && CoversStatus(parsed);

    /// <summary>What's still owed: the price less what's covered, never below zero. No price means nothing owed.</summary>
    public static decimal Due(decimal? price, decimal covered) => Math.Max(0m, (price ?? 0m) - covered);

    /// <summary>"Covered" (stored as IsPaid): a price is set and the covering payments reach it. A 0 price is covered.</summary>
    public static bool IsCovered(decimal? price, decimal covered) => price.HasValue && covered >= price.Value;
}
