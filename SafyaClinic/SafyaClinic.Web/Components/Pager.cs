using System.Globalization;
using SafyaClinic.Application.DTOs.Common;

namespace SafyaClinic.Web.Components;

/// <summary>
/// F8c: what the pagination component needs. Use with
/// <c>&lt;partial name="_AppPagination" model="PagerModel.From(Model)" /&gt;</c>.
/// </summary>
public sealed record PagerModel(int Page, int PageSize, int TotalCount, string Action = "Index", string PageParameter = "page")
{
    public int TotalPages => PageSize < 1 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public int First => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int Last => Math.Min(Page * PageSize, TotalCount);
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;

    /// <summary>The numbered pages shown around the current one.</summary>
    public IReadOnlyList<int> Window
    {
        get
        {
            var from = Math.Max(1, Page - 2);
            var to = Math.Min(TotalPages, Page + 2);
            return to < from ? Array.Empty<int>() : Enumerable.Range(from, to - from + 1).ToArray();
        }
    }

    /// <summary>"Showing 21–40 of 132", with Western digits.</summary>
    public string Summary => TotalCount == 0
        ? "No results"
        : string.Format(CultureInfo.InvariantCulture, "Showing {0:N0}–{1:N0} of {2:N0}", First, Last, TotalCount);

    public static PagerModel From<T>(PagedResult<T> result, string action = "Index") =>
        new(result.Page, result.PageSize, result.TotalCount, action);
}

/// <summary>
/// F8c: an empty state with one next action. Use with
/// <c>&lt;partial name="_AppEmpty" model="new EmptyStateModel(&quot;No reservations on this day&quot;)" /&gt;</c>.
/// </summary>
public sealed record EmptyStateModel(
    string Message,
    string Icon = "bi-inbox",
    string? Hint = null,
    string? ActionText = null,
    string? ActionUrl = null);
