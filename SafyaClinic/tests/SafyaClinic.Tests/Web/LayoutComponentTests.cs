using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Web.Components;
using SafyaClinic.Web.TagHelpers;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F8c: pagination, data table, card, KPI tile, tabs and filter bar.</summary>
public class LayoutComponentTests
{
    private static TagHelperContext Context(Dictionary<object, object>? items = null) =>
        new(new TagHelperAttributeList(), items ?? new Dictionary<object, object>(), Guid.NewGuid().ToString("N"));

    private static TagHelperOutput Output(string tag, string childHtml = "") =>
        new(tag, new TagHelperAttributeList(), (_, _) =>
        {
            var content = new DefaultTagHelperContent();
            content.SetHtmlContent(childHtml);
            return Task.FromResult<TagHelperContent>(content);
        });

    private static string Render(TagHelperOutput output)
    {
        using var writer = new StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return System.Net.WebUtility.HtmlDecode(writer.ToString());
    }

    // ── Pagination ──────────────────────────────────────────────

    [Fact]
    public void Pager_says_which_rows_are_shown()
    {
        Assert.Equal("Showing 21–40 of 132", new PagerModel(2, 20, 132).Summary);
        Assert.Equal("Showing 121–132 of 132", new PagerModel(7, 20, 132).Summary);
        Assert.Equal("Showing 1–5 of 5", new PagerModel(1, 20, 5).Summary);
        Assert.Equal("Showing 1–20 of 1,500", new PagerModel(1, 20, 1500).Summary);
    }

    [Fact]
    public void Pager_shows_two_pages_either_side_of_the_current_one()
    {
        var pager = new PagerModel(5, 20, 200);

        Assert.Equal(10, pager.TotalPages);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, pager.Window);
        Assert.True(pager.HasPrev);
        Assert.True(pager.HasNext);
        Assert.Equal(new[] { 1, 2, 3 }, new PagerModel(1, 20, 60).Window);
    }

    [Fact]
    public void Pager_with_no_results_has_no_pages()
    {
        var pager = new PagerModel(1, 20, 0);

        Assert.Equal(0, pager.TotalPages);
        Assert.Empty(pager.Window);
        Assert.False(pager.HasNext);
        Assert.Equal("No results", pager.Summary);
    }

    [Fact]
    public void Pager_is_built_from_a_paged_result()
    {
        var result = new PagedResult<string> { Page = 3, PageSize = 10, TotalCount = 45 };

        var pager = PagerModel.From(result, "Search");

        Assert.Equal(new PagerModel(3, 10, 45, "Search"), pager);
    }

    [Fact]
    public void No_view_uses_the_old_pagination_partial()
    {
        var views = Views();

        Assert.False(File.Exists(Path.Combine(views, "Shared", "_Pagination.cshtml")),
            "_Pagination.cshtml was replaced by _AppPagination.cshtml; remove it.");
        var users = Directory.GetFiles(views, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("\"_Pagination\""))
            .Select(Path.GetFileName);
        Assert.Empty(users);
    }

    // ── Data table ──────────────────────────────────────────────

    [Fact]
    public void Table_without_a_caption_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new AppTableTagHelper().Process(Context(), Output("app-table")));
    }

    [Fact]
    public void Table_is_a_named_focusable_region_with_a_caption()
    {
        var output = Output("app-table");
        new AppTableTagHelper { Caption = "Patient results", CaptionHidden = true, Stack = true }.Process(Context(), output);

        var html = Render(output);
        Assert.StartsWith("<div", html);
        Assert.Contains("role=\"region\"", html);
        Assert.Contains("aria-label=\"Patient results\"", html);
        Assert.Contains("tabindex=\"0\"", html);
        Assert.Contains("<table class=\"app-table app-table--stack\">", html);
        Assert.Contains("<caption class=\"visually-hidden\">Patient results</caption>", html);
        Assert.EndsWith("</table></div>", html);
    }

    // ── Card ────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Card_heading_level_must_be_2_to_4(int level)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AppCardTagHelper { Title = "Payments", Level = level }.ProcessAsync(Context(), Output("app-card")));
    }

    [Fact]
    public async Task Card_is_a_section_named_by_its_heading()
    {
        var output = Output("app-card", "<p>Body</p>");
        await new AppCardTagHelper { Title = "Today's payments", Level = 3, Id = "payments" }.ProcessAsync(Context(), output);

        var html = Render(output);
        Assert.StartsWith("<section", html);
        Assert.Contains("aria-labelledby=\"payments-title\"", html);
        Assert.Contains("<h3 class=\"app-card__title\" id=\"payments-title\">Today's payments</h3>", html);
        Assert.Contains("<div class=\"app-card__body\"><p>Body</p></div>", html);
        Assert.DoesNotContain("app-card__actions", html);
    }

    // ── KPI tile ────────────────────────────────────────────────

    [Fact]
    public void Kpi_without_a_label_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new AppKpiTagHelper { Value = "7" }.Process(Context(), Output("app-kpi")));
    }

    [Fact]
    public void Kpi_with_a_link_is_one_link_that_includes_label_and_value()
    {
        var output = Output("app-kpi");
        new AppKpiTagHelper { Label = "Waiting now", Value = "7", Href = "/Reservations/Queue", Tone = "warning" }
            .Process(Context(), output);

        var html = Render(output);
        Assert.StartsWith("<a", html);
        Assert.Contains("href=\"/Reservations/Queue\"", html);
        Assert.Contains("app-kpi--warning", html);
        Assert.Contains(">Waiting now<", html);
        Assert.Contains(">7<", html);
    }

    [Fact]
    public void Kpi_with_an_unknown_tone_is_neutral()
    {
        var output = Output("app-kpi");
        new AppKpiTagHelper { Label = "Patients", Value = "12", Tone = "purple" }.Process(Context(), output);

        Assert.Contains("app-kpi app-kpi--neutral", Render(output));
    }

    // ── Tabs ────────────────────────────────────────────────────

    [Fact]
    public async Task Tabs_select_the_first_tab_unless_another_is_named()
    {
        var html = await RenderTabsAsync(selected: null);
        Assert.Contains("id=\"p-details-tab\" aria-controls=\"p-details-panel\" aria-selected=\"true\" tabindex=\"0\"", html);
        Assert.Contains("id=\"p-visits-tab\" aria-controls=\"p-visits-panel\" aria-selected=\"false\" tabindex=\"-1\"", html);

        html = await RenderTabsAsync(selected: "visits");
        Assert.Contains("id=\"p-details-tab\" aria-controls=\"p-details-panel\" aria-selected=\"false\"", html);
        Assert.Contains("id=\"p-visits-tab\" aria-controls=\"p-visits-panel\" aria-selected=\"true\"", html);
    }

    [Fact]
    public void Tab_panels_are_labelled_by_their_tab_and_hidden_unless_selected()
    {
        var items = new Dictionary<object, object>
        {
            [typeof(AppTabsTagHelper)] = new AppTabsTagHelper.State { Prefix = "p" },
        };
        var first = Output("app-tab", "Details body");
        var second = Output("app-tab", "Visits body");
        new AppTabTagHelper { Id = "details", Title = "Details" }.Process(Context(items), first);
        new AppTabTagHelper { Id = "visits", Title = "Visits" }.Process(Context(items), second);

        var one = Render(first);
        var two = Render(second);
        Assert.Contains("role=\"tabpanel\"", one);
        Assert.Contains("aria-labelledby=\"p-details-tab\"", one);
        Assert.DoesNotContain("hidden", one);
        Assert.Contains("hidden=\"hidden\"", two);
    }

    [Fact]
    public async Task Tabs_without_a_label_are_refused()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AppTabsTagHelper().ProcessAsync(Context(), Output("app-tabs")));
    }

    // ── Filter bar ──────────────────────────────────────────────

    [Fact]
    public void Filter_bar_is_a_named_search_form_with_search_and_clear()
    {
        var output = Output("app-filter-bar");
        new AppFilterBarTagHelper { Label = "Find patients", ClearUrl = "/Patients" }.Process(Context(), output);

        var html = Render(output);
        Assert.StartsWith("<form", html);
        Assert.Contains("method=\"get\"", html);
        Assert.Contains("role=\"search\"", html);
        Assert.Contains("aria-label=\"Find patients\"", html);
        Assert.Contains("type=\"submit\"", html);
        Assert.Contains("href=\"/Patients\">Clear</a>", html);
    }

    [Fact]
    public void Filter_bar_without_a_label_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new AppFilterBarTagHelper().Process(Context(), Output("app-filter-bar")));
    }

    // ── helpers ─────────────────────────────────────────────────

    /// <summary>Runs app-tabs with two app-tab children, the way Razor does (children share the parent's items).</summary>
    private static async Task<string> RenderTabsAsync(string? selected)
    {
        var items = new Dictionary<object, object>();
        var output = new TagHelperOutput("app-tabs", new TagHelperAttributeList(), (_, _) =>
        {
            var panels = new DefaultTagHelperContent();
            foreach (var (id, title) in new[] { ("details", "Details"), ("visits", "Visits") })
            {
                var child = Output("app-tab", title + " body");
                new AppTabTagHelper { Id = id, Title = title }.Process(Context(items), child);
                panels.AppendHtml(child);
            }
            return Task.FromResult<TagHelperContent>(panels);
        });

        await new AppTabsTagHelper { Label = "Patient sections", Id = "p", Selected = selected }.ProcessAsync(Context(items), output);
        return Render(output);
    }

    private static string Views([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "SafyaClinic.Web", "Views"));
}
