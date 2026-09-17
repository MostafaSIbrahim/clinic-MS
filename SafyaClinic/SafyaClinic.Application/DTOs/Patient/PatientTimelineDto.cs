using SafyaClinic.Application.DTOs.Common;

namespace SafyaClinic.Application.DTOs.Patient;

public class PatientTimelineDto
{
    public int PatientId { get; init; }

    public string PatientName { get; init; } = string.Empty;

    public PagedResult<PatientTimelineItemDto> Timeline { get; init; } = new()
    {
        Page = 1,
        PageSize = 20
    };
}