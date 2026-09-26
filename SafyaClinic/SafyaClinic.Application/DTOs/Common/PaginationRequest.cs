namespace SafyaClinic.Application.DTOs.Common;

public class PaginationRequest
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize :
                            value > MaxPageSize ? MaxPageSize : value;
    }

    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }
}