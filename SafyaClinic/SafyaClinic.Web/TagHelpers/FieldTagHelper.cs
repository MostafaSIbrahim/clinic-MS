using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SafyaClinic.Web.TagHelpers;

/// <summary>
/// F8: one form field — label, control, optional hint and the validation message, wired together
/// with for/id and aria-describedby. A field cannot be rendered without a label.
/// <code>
/// &lt;app-field asp-for="FullName" label="Full name" required="true" /&gt;
/// &lt;app-field asp-for="Email" label="Email" type="email" hint="Used for password resets." /&gt;
/// &lt;app-field asp-for="ClinicId" label="Clinic" type="select" asp-items="clinics" option-label="Choose…" /&gt;
/// &lt;app-field asp-for="Notes" label="Notes" type="textarea" rows="4" /&gt;
/// </code>
/// Types: text (default), email, tel, number, date, time, datetime-local, password, textarea, select.
/// </summary>
[HtmlTargetElement("app-field", TagStructure = TagStructure.WithoutEndTag)]
public class FieldTagHelper : TagHelper
{
    private readonly IHtmlGenerator _generator;

    public FieldTagHelper(IHtmlGenerator generator) => _generator = generator;

    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }
    [HtmlAttributeName("label")] public string? Label { get; set; }
    [HtmlAttributeName("hint")] public string? Hint { get; set; }
    [HtmlAttributeName("type")] public string? Type { get; set; }
    [HtmlAttributeName("format")] public string? Format { get; set; }
    [HtmlAttributeName("asp-items")] public IEnumerable<SelectListItem>? Items { get; set; }
    [HtmlAttributeName("option-label")] public string? OptionLabel { get; set; }
    [HtmlAttributeName("placeholder")] public string? Placeholder { get; set; }
    [HtmlAttributeName("required")] public bool Required { get; set; }
    [HtmlAttributeName("rows")] public int Rows { get; set; } = 3;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Label))
            throw new InvalidOperationException($"<app-field asp-for=\"{For?.Name}\"> needs a label.");
        if (For is null)
            throw new InvalidOperationException($"<app-field label=\"{Label}\"> needs asp-for.");

        var name = For.Name;
        var fullName = ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(name);
        var id = TagBuilder.CreateSanitizedId(fullName, "_");
        var hintId = id + "-hint";
        var errorId = id + "-error";
        var type = (Type ?? "text").ToLowerInvariant();

        var control = type switch
        {
            "textarea" => _generator.GenerateTextArea(ViewContext, For.ModelExplorer, name, Rows, 0,
                new { @class = "form-control" }),
            "select" => _generator.GenerateSelect(ViewContext, For.ModelExplorer, OptionLabel, name,
                Items ?? Enumerable.Empty<SelectListItem>(), allowMultiple: false, new { @class = "form-select" }),
            "password" => _generator.GeneratePassword(ViewContext, For.ModelExplorer, name, null,
                new { @class = "form-control", autocomplete = "new-password" }),
            _ => _generator.GenerateTextBox(ViewContext, For.ModelExplorer, name, For.Model,
                Format ?? DefaultFormat(type), new { @class = "form-control", type }),
        };

        control.MergeAttribute("aria-describedby", Hint is null ? errorId : $"{hintId} {errorId}", replaceExisting: true);
        if (Required) control.MergeAttribute("required", "required");
        if (Placeholder is not null) control.MergeAttribute("placeholder", Placeholder);
        if (ViewContext.ViewData.ModelState.TryGetValue(fullName, out var entry) && entry.Errors.Count > 0)
            control.MergeAttribute("aria-invalid", "true");

        var label = _generator.GenerateLabel(ViewContext, For.ModelExplorer, name, Label, new { @class = "form-label" });
        if (Required) label.InnerHtml.AppendHtml("<span class=\"app-required\" aria-hidden=\"true\"> *</span>");

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "app-field");
        output.Content.AppendHtml(label);
        output.Content.AppendHtml(control);
        if (Hint is not null)
            output.Content.AppendHtml($"<div id=\"{hintId}\" class=\"form-text\">{System.Net.WebUtility.HtmlEncode(Hint)}</div>");

        var error = _generator.GenerateValidationMessage(ViewContext, For.ModelExplorer, name, null, "div",
            new { @class = "app-field-error", id = errorId });
        if (error is not null) output.Content.AppendHtml(error);
    }

    private static string? DefaultFormat(string type) => type switch
    {
        "date" => "{0:yyyy-MM-dd}",
        "datetime-local" => "{0:yyyy-MM-ddTHH:mm}",
        _ => null
    };
}
