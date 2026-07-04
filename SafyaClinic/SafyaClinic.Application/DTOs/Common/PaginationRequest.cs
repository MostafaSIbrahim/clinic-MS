namespace SafyaClinic.Application.DTOs.Common;

public class PaginationRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? 20 : value > 100 ? 100 : value;
    }

    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; }
}