using Microsoft.AspNetCore.Razor.TagHelpers;
using SafyaClinic.Application.Common;
using SafyaClinic.Web.TagHelpers;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F8: display and button tag helpers; fields and icon buttons refuse to render without a label.</summary>
public class TagHelperTests
{
    private static (TagHelperContext Context, TagHelperOutput Output) Make(string tag) =>
        (new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test"),
         new TagHelperOutput(tag, new TagHelperAttributeList(),
             (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent())));

    private static string Render(TagHelperOutput output)
    {
        using var writer = new StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public void Money_is_shown_with_currency_and_western_digits()
    {
        var (context, output) = Make("app-money");
        new MoneyTagHelper { Value = 1250.5m }.Process(context, output);

        Assert.Equal("<span class=\"app-money\">1,250.50 EGP</span>", Render(output));
    }

    [Fact]
    public void Missing_money_shows_a_dash()
    {
        var (context, output) = Make("app-money");
        new MoneyTagHelper { Value = null }.Process(context, output);

        Assert.Equal("—", System.Net.WebUtility.HtmlDecode(output.Content.GetContent()));
    }

    [Fact]
    public void Utc_time_is_shown_in_clinic_time()
    {
        var (context, output) = Make("app-datetime");
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId));

        // 22:30 UTC on 1 Dec is 00:30 on 2 Dec in Cairo (UTC+2 in winter).
        new DateTimeTagHelper(clock) { Utc = new DateTime(2026, 12, 1, 22, 30, 0) }.Process(context, output);

        var html = Render(output);
        Assert.StartsWith("<time", html);
        Assert.Contains("datetime=\"2026-12-02T00:30\"", html);
        Assert.Contains(">02 Dec 2026 00:30<", html);
    }

    [Fact]
    public void Icon_button_has_an_accessible_name()
    {
        var (context, output) = Make("app-icon-button");
        new IconButtonTagHelper { Icon = "bi-trash", Label = "Delete attachment", Variant = "outline-danger", Type = "submit" }
            .Process(context, output);

        var html = Render(output);
        Assert.Contains("type=\"submit\"", html);
        Assert.Contains("btn-outline-danger", html);
        Assert.Contains("<span class=\"visually-hidden\">Delete attachment</span>", html);
        Assert.Contains("aria-hidden=\"true\"", html);
    }

    [Fact]
    public void Icon_button_without_a_label_is_refused()
    {
        var (context, output) = Make("app-icon-button");

        Assert.Throws<InvalidOperationException>(() => new IconButtonTagHelper { Icon = "bi-trash" }.Process(context, output));
    }

    [Fact]
    public void Field_without_a_label_is_refused()
    {
        var (context, output) = Make("app-field");

        Assert.Throws<InvalidOperationException>(() => new FieldTagHelper(null!).Process(context, output));
    }
}
