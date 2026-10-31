using System.Reflection;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Tests.Web;

/// <summary>Test double for IReservationService: today's list is empty.</summary>
public class ReservationServiceStub : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
    {
        "GetTodayReservationsAsync" =>
            Task.FromResult(ServiceResult<IEnumerable<ReservationSummaryDto>>.Success(Array.Empty<ReservationSummaryDto>())),
        _ => throw new NotSupportedException(targetMethod.Name)
    };
}