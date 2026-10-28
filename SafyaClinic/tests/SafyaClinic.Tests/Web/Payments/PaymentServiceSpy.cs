using System.Reflection;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;

namespace SafyaClinic.Tests.Web.Payments;

/// <summary>
/// Test double for IPaymentService: records every call and returns empty
/// successful results, so controller rules can be tested without a database.
/// </summary>
public class PaymentServiceSpy : DispatchProxy
{
    public List<(string Name, object?[] Args)> Calls { get; } = new();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Calls.Add((targetMethod!.Name, args ?? Array.Empty<object?>()));

        return targetMethod.Name switch
        {
            "GetPaymentsByDateRangeAsync" =>
                Task.FromResult(ServiceResult<IEnumerable<PaymentDto>>.Success(Array.Empty<PaymentDto>())),
            "GetPaymentDashboardAsync" =>
                Task.FromResult(ServiceResult<PaymentDashboardDto>.Success(new PaymentDashboardDto())),
            _ => throw new NotSupportedException(targetMethod.Name)
        };
    }
}