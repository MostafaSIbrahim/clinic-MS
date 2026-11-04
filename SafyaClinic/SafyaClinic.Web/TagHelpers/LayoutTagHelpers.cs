using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SafyaClinic.Web.TagHelpers;

/// <summary>
/// F8c: the data table (Direction C). A caption is required; it names the scroll region too.
/// <code>
/// &lt;app-table caption="Patients" caption-hidden="true" stack="true"&gt;
///     &lt;thead&gt;…&lt;/thead&gt;&lt;tbody&gt;…&lt;td data-label="Phone"&gt;…&lt;/tbody&gt;
/// &lt;/app-table&gt;
/// </code>
/// With <c>stack="true"</c>, rows become cards below 768 px; each cell shows its <c>data-label</c>.
/// </summary>
[HtmlTargetElement("app-table")]
public class AppTableTagHelper : TagHelper
{
    public string? Caption { get; set; }

    /// <summary>Hide the caption visually when a heading above already names the table.</summary>
    public bool CaptionHidden { get; set; }

    /// <summary>Show rows as stacked cards on phones (priority lists).</summary>
    public bool Stack { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Caption))
            throw new InvalidOperationException("<app-table> needs a caption.");

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-table-wrap");
        output.Attributes.SetAttribute("role", "region");
        output.Attributes.SetAttribute("aria-label", Caption);
        output.Attributes.SetAttribute("tabindex", "0");

        output.PreContent.AppendHtml(Stack ? "<table class=\"app-table app-table--stack\">" : "<table class=\"app-table\">");
        output.PreContent.AppendHtml(CaptionHidden ? "<caption class=\"visually-hidden\">" : "<caption>");
        output.PreContent.Append(Caption);
        output.PreContent.AppendHtml("</caption>");
        output.PostContent.AppendHtml("</table>");
    }
}

/// <summary>
/// F8c: a card with a real heading. <c>level</c> is 2, 3 or 4 (the page has the h1).
/// <code>
/// &lt;app-card title="Today's payments" level="2" icon="bi-cash"&gt;
///     &lt;app-card-actions&gt;&lt;a class="btn btn-sm btn-outline-secondary" href="…"&gt;See all&lt;/a&gt;&lt;/app-card-actions&gt;
///     …body…
/// &lt;/app-card&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-card")]
public class AppCardTagHelper : TagHelper
{
    public sealed class Slots { public TagHelperContent? Actions { get; set; } }

    public string? Title { get; set; }
    public int Level { get; set; } = 2;
    public string? Icon { get; set; }
    public string? Id { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Title))
            throw new InvalidOperationException("<app-card> needs a title.");
        if (Level is < 2 or > 4)
            throw new InvalidOperationException("<app-card> level must be 2, 3 or 4.");

        var slots = new Slots();
        context.Items[typeof(AppCardTagHelper)] = slots;
        var body = await output.GetChildContentAsync();

        var headingId = TagBuilder.CreateSanitizedId(Id ?? "app-card-" + context.UniqueId, "-") + "-title";
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-card");
        output.Attributes.SetAttribute("aria-labelledby", headingId);
        if (Id is not null) output.Attributes.SetAttribute("id", Id);

        output.Content.AppendHtml("<header class=\"app-card__header\">");
        output.Content.AppendHtml($"<h{Level} class=\"app-card__title\" id=\"{headingId}\">");
        if (!string.IsNullOrWhiteSpace(Icon))
        {
            output.Content.AppendHtml("<i class=\"bi ");
            output.Content.Append(Icon);
            output.Content.AppendHtml("\" aria-hidden=\"true\"></i> ");
        }
        output.Content.Append(Title);
        output.Content.AppendHtml($"</h{Level}>");
        if (slots.Actions is { IsEmptyOrWhiteSpace: false })
        {
            output.Content.AppendHtml("<div class=\"app-card__actions\">");
            output.Content.AppendHtml(slots.Actions);
            output.Content.AppendHtml("</div>");
        }
        output.Content.AppendHtml("</header><div class=\"app-card__body\">");
        output.Content.AppendHtml(body);
        output.Content.AppendHtml("</div>");
    }
}

/// <summary>F8c: the actions shown in a card's header, next to its title.</summary>
[HtmlTargetElement("app-card-actions", ParentTag = "app-card")]
public class AppCardActionsTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = await output.GetChildContentAsync();
        if (context.Items.TryGetValue(typeof(AppCardTagHelper), out var value) && value is AppCardTagHelper.Slots slots)
            slots.Actions = content;
        output.SuppressOutput();
    }
}

/// <summary>
/// F8c: a KPI tile. With <c>href</c>, the whole tile is one link.
/// <c>&lt;app-kpi label="Waiting now" value="7" icon="bi-hourglass" tone="warning" href="…" hint="2 over 30 min" /&gt;</c>
/// </summary>
[HtmlTargetElement("app-kpi", TagStructure = TagStructure.WithoutEndTag)]
public class AppKpiTagHelper : TagHelper
{
    private static readonly HashSet<string> Tones = new(StringComparer.Ordinal)
        { "neutral", "primary", "success", "warning", "danger" };

    public string? Label { get; set; }
    public string? Value { get; set; }
    public string? Icon { get; set; }
    public string? Hint { get; set; }
    public string? Href { get; set; }
    public string Tone { get; set; } = "neutral";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException("<app-kpi> needs a label.");
        var tone = Tones.Contains(Tone) ? Tone : "neutral";

        output.TagName = string.IsNullOrWhiteSpace(Href) ? "div" : "a";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"app-kpi app-kpi--{tone}");
        if (!string.IsNullOrWhiteSpace(Href)) output.Attributes.SetAttribute("href", Href);

        output.Content.AppendHtml("<span class=\"app-kpi__label\">");
        if (!string.IsNullOrWhiteSpace(Icon))
        {
            output.Content.AppendHtml("<i class=\"bi ");
            output.Content.Append(Icon);
            output.Content.AppendHtml("\" aria-hidden=\"true\"></i> ");
        }
        output.Content.Append(Label);
        output.Content.AppendHtml("</span><span class=\"app-kpi__value\">");
        output.Content.Append(string.IsNullOrWhiteSpace(Value) ? "—" : Value);
        output.Content.AppendHtml("</span>");
        if (!string.IsNullOrWhiteSpace(Hint))
        {
            output.Content.AppendHtml("<span class=\"app-kpi__hint\">");
            output.Content.Append(Hint);
            output.Content.AppendHtml("</span>");
        }
    }
}

/// <summary>
/// F8c: tabs (ARIA tabs pattern; arrow keys, Home and End are handled by app-tabs.js).
/// The first tab is selected unless <c>selected</c> names another.
/// <code>
/// &lt;app-tabs label="Patient sections" id="patient" selected="visits"&gt;
///     &lt;app-tab id="details" title="Details"&gt;…&lt;/app-tab&gt;
///     &lt;app-tab id="visits" title="Visits"&gt;…&lt;/app-tab&gt;
/// &lt;/app-tabs&gt;
/// </code>
/// For sections that are separate pages, use links with aria-current instead.
/// </summary>
[HtmlTargetElement("app-tabs")]
public class AppTabsTagHelper : TagHelper
{
    public sealed record Tab(string TabId, string PanelId, string Title, bool Selected);

    public sealed class State
    {
        public required string Prefix { get; init; }
        public string? Selected { get; init; }
        public List<Tab> Tabs { get; } = new();
    }

    public string? Label { get; set; }
    public string Id { get; set; } = "tabs";
    public string? Selected { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException("<app-tabs> needs a label.");

        var state = new State { Prefix = TagBuilder.CreateSanitizedId(Id, "-"), Selected = Selected };
        context.Items[typeof(AppTabsTagHelper)] = state;
        var panels = await output.GetChildContentAsync();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-tabs");
        output.Attributes.SetAttribute("id", state.Prefix);

        output.Content.AppendHtml("<div class=\"app-tabs__list\" role=\"tablist\" aria-label=\"");
        output.Content.Append(Label);
        output.Content.AppendHtml("\">");
        foreach (var tab in state.Tabs)
        {
            output.Content.AppendHtml(
                $"<button type=\"button\" class=\"app-tabs__tab\" role=\"tab\" id=\"{tab.TabId}\" " +
                $"aria-controls=\"{tab.PanelId}\" aria-selected=\"{(tab.Selected ? "true" : "false")}\" " +
                $"tabindex=\"{(tab.Selected ? "0" : "-1")}\">");
            output.Content.Append(tab.Title);
            output.Content.AppendHtml("</button>");
        }
        output.Content.AppendHtml("</div>");
        output.Content.AppendHtml(panels);
    }
}

/// <summary>F8c: one tab and its panel, inside &lt;app-tabs&gt;.</summary>
[HtmlTargetElement("app-tab", ParentTag = "app-tabs")]
public class AppTabTagHelper : TagHelper
{
    public string? Id { get; set; }
    public string? Title { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title))
            throw new InvalidOperationException("<app-tab> needs an id and a title.");
        if (!context.Items.TryGetValue(typeof(AppTabsTagHelper), out var value) || value is not AppTabsTagHelper.State state)
            throw new InvalidOperationException("<app-tab> must be inside <app-tabs>.");

        var key = TagBuilder.CreateSanitizedId(Id, "-");
        var selected = state.Selected is null ? state.Tabs.Count == 0 : state.Selected == Id;
        var tab = new AppTabsTagHelper.Tab($"{state.Prefix}-{key}-tab", $"{state.Prefix}-{key}-panel", Title, selected);
        state.Tabs.Add(tab);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-tabs__panel");
        output.Attributes.SetAttribute("role", "tabpanel");
        output.Attributes.SetAttribute("id", tab.PanelId);
        output.Attributes.SetAttribute("aria-labelledby", tab.TabId);
        output.Attributes.SetAttribute("tabindex", "0");
        if (!selected) output.Attributes.SetAttribute("hidden", "hidden");
    }
}

/// <summary>
/// F8c: a filter bar. A GET form named for screen readers, with Search and Clear.
/// <code>
/// &lt;app-filter-bar label="Find patients"&gt;
///     &lt;div class="app-filter-bar__field"&gt;&lt;label …&gt;…&lt;/label&gt;&lt;input …&gt;&lt;/div&gt;
/// &lt;/app-filter-bar&gt;
/// </code>
/// It submits to the current page. Clear goes back to the page without filters.
/// </summary>
[HtmlTargetElement("app-filter-bar")]
public class AppFilterBarTagHelper : TagHelper
{
    public string? Label { get; set; }
    public string SubmitText { get; set; } = "Search";
    public string? ClearUrl { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException("<app-filter-bar> needs a label.");

        var request = ViewContext?.HttpContext.Request;
        var clear = ClearUrl ?? (request is null ? "?" : (request.PathBase + request.Path).ToString());

        output.TagName = "form";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("method", "get");
        output.Attributes.SetAttribute("role", "search");
        output.Attributes.SetAttribute("aria-label", Label);
        output.Attributes.SetAttribute("class", "app-filter-bar");

        output.PostContent.AppendHtml("<div class=\"app-filter-bar__actions\">");
        output.PostContent.AppendHtml("<button type=\"submit\" class=\"btn btn-primary\"><i class=\"bi bi-search\" aria-hidden=\"true\"></i> ");
        output.PostContent.Append(SubmitText);
        output.PostContent.AppendHtml("</button><a class=\"btn btn-outline-secondary\" href=\"");
        output.PostContent.Append(clear);
        output.PostContent.AppendHtml("\">Clear</a></div>");
    }
}
