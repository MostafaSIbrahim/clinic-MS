using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SafyaClinic.Application.Common;

namespace SafyaClinic.Web.TagHelpers;

/// <summary>F8: money as "1,250.50 EGP" (Western digits, tabular). <c>&lt;app-money value="@x" /&gt;</c></summary>
[HtmlTargetElement("app-money", TagStructure = TagStructure.WithoutEndTag)]
public class MoneyTagHelper : TagHelper
{
    public decimal? Value { get; set; }

    /// <summary>Shown when there is no value.</summary>
    public string Empty { get; set; } = "—";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-money");
        output.Content.SetContent(Value is decimal amount ? Money.Format(amount) : Empty);
    }
}

/// <summary>
/// F8: a stored UTC time shown in clinic time, in a &lt;time&gt; element.
/// <c>&lt;app-datetime utc="@x" /&gt;</c>, optionally <c>format="dd MMM yyyy"</c>.
/// </summary>
[HtmlTargetElement("app-datetime", TagStructure = TagStructure.WithoutEndTag)]
public class DateTimeTagHelper : TagHelper
{
    public const string DefaultFormat = "dd MMM yyyy HH:mm";
    private readonly IClinicClock _clock;

    public DateTimeTagHelper(IClinicClock clock) => _clock = clock;

    public DateTime? Utc { get; set; }
    public string Format { get; set; } = DefaultFormat;
    public string Empty { get; set; } = "—";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = Utc is null ? "span" : "time";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-datetime");
        if (Utc is not DateTime utc)
        {
            output.Content.SetContent(Empty);
            return;
        }

        var local = _clock.ToClinicTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        output.Attributes.SetAttribute("datetime", local.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
        output.Content.SetContent(local.ToString(Format, CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// F8: an icon-only button that always has an accessible name.
/// <c>&lt;app-icon-button icon="bi-trash" label="Delete attachment" variant="outline-danger" type="submit" /&gt;</c>
/// Other attributes (data-confirm, formaction, name, value…) pass through.
/// </summary>
[HtmlTargetElement("app-icon-button", TagStructure = TagStructure.WithoutEndTag)]
public class IconButtonTagHelper : TagHelper
{
    public string? Icon { get; set; }
    public string? Label { get; set; }
    public string Variant { get; set; } = "outline-secondary";
    public string Type { get; set; } = "button";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException("<app-icon-button> needs a label (it is the button's name for screen readers).");
        if (string.IsNullOrWhiteSpace(Icon))
            throw new InvalidOperationException("<app-icon-button> needs an icon.");

        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", Type);
        output.Attributes.SetAttribute("class", $"btn btn-sm btn-{Variant} app-icon-button");
        output.Attributes.SetAttribute("title", Label);
        output.Content.SetHtmlContent(
            $"<i class=\"bi {System.Net.WebUtility.HtmlEncode(Icon)}\" aria-hidden=\"true\"></i>" +
            $"<span class=\"visually-hidden\">{System.Net.WebUtility.HtmlEncode(Label)}</span>");
    }
}
