using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Options;
using SafyaClinic.Application.Services.EgyptianDrugIndex;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P8b: the built-in drug list and how it's searched.</summary>
public class DrugIndexTests
{
    private static DrugEntry Drug(string name, string arabic = "", string scientific = "") =>
        new(name, arabic, scientific, "Maker", "Class", "ORAL.SOLID", 10m);

    private static readonly DrugIndex Sample = new(new[]
    {
        Drug("AUGMENTIN 1 GM 14 F.C.TABS.", "أوجمنتين", "AMOXICILLIN+CLAVULANIC ACID"),
        Drug("AUGMENTIN 625 MG 10 F.C.TABS.", "أوجمنتين", "AMOXICILLIN+CLAVULANIC ACID"),
        Drug("HIBIOTIC 1 GM 16 F.C.TABS.", "هاي بيوتك", "AMOXICILLIN+CLAVULANIC ACID"),
        Drug("CO-AUGMENTIN 375 MG", "كو", "AMOXICILLIN+CLAVULANIC ACID"),
        Drug("PANADOL 500 MG 24 TABS.", "بنادول", "PARACETAMOL(ACETAMINOPHEN)"),
    });

    [Fact]
    public void Names_starting_with_the_text_come_first_then_word_matches_then_ingredient_matches()
    {
        var names = Sample.Find("augmentin", 10).Select(d => d.Name).ToList();

        Assert.Equal(new[] { "AUGMENTIN 1 GM 14 F.C.TABS.", "AUGMENTIN 625 MG 10 F.C.TABS.", "CO-AUGMENTIN 375 MG" }, names);
    }

    [Fact]
    public void Every_word_must_match_in_any_order()
    {
        Assert.Equal("AUGMENTIN 625 MG 10 F.C.TABS.", Assert.Single(Sample.Find("625 augmentin", 10)).Name);
        Assert.Empty(Sample.Find("augmentin 250", 10));
    }

    [Fact]
    public void The_active_ingredient_finds_every_brand()
    {
        var names = Sample.Find("amoxicillin", 10).Select(d => d.Name).ToList();

        Assert.Equal(4, names.Count);
        Assert.Contains("HIBIOTIC 1 GM 16 F.C.TABS.", names);
    }

    [Theory]
    [InlineData("اوجمنتين")]   // without the hamza
    [InlineData("أوجمنتين")]
    [InlineData("بَنادول")]      // with a diacritic
    public void Arabic_names_match_without_diacritics_or_hamza(string query)
    {
        Assert.NotEmpty(Sample.Find(query, 10));
    }

    [Fact]
    public void The_number_of_results_is_limited_and_empty_text_finds_nothing()
    {
        Assert.Single(Sample.Find("amoxicillin", 1));
        Assert.Empty(Sample.Find("   ", 10));
    }

    [Fact]
    public async Task The_built_in_list_holds_the_Egyptian_market()
    {
        Assert.True(LocalEgyptianDrugSource.List.Count > 20_000, $"Only {LocalEgyptianDrugSource.List.Count} drugs loaded.");

        var source = new LocalEgyptianDrugSource(Options.Create(new EgyptianDrugIndexOptions { MaxResults = 15 }),
            NullLogger<LocalEgyptianDrugSource>.Instance);
        var found = (await source.FetchAsync("augmentin", CancellationToken.None)).ToList();

        Assert.InRange(found.Count, 5, 15);
        Assert.All(found, d => Assert.StartsWith("AUGMENTIN", d.TradeName));
        Assert.Contains(found, d => d.PublicPrice > 0 && d.ArabicName.Length > 0 && d.ScientificName.Contains("AMOXICILLIN"));
    }
}
