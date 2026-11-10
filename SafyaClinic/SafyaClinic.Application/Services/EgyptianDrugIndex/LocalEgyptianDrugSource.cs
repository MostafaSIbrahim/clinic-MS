using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.DTOs.Medicine;
using SafyaClinic.Application.Options;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

// P8b: the default source. A public-domain (CC0) list of about 25,000 medicines registered in Egypt, compiled by
// the open "egyptian-drug-database" project (see Data/egyptian-drugs.SOURCE.md). It's built into this assembly,
// so it works on any host with no outside service, file path or API key. Prices are a snapshot and the Arabic
// names are the compiler's spellings: suggestions help typing, and the doctor still checks the name.
public class LocalEgyptianDrugSource : IEgyptianDrugSource
{
    public const string ResourceName = "SafyaClinic.Drugs.json.gz";

    private static readonly Lazy<DrugIndex> Index = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly EgyptianDrugIndexOptions _options;
    private readonly ILogger<LocalEgyptianDrugSource> _logger;

    public LocalEgyptianDrugSource(IOptions<EgyptianDrugIndexOptions> options, ILogger<LocalEgyptianDrugSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The whole list, loaded once.</summary>
    public static DrugIndex List => Index.Value;

    public Task<IEnumerable<EgyptianMedicineDto>> FetchAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        var max = _options.MaxResults > 0 ? _options.MaxResults : 15;
        var found = List.Find(normalizedQuery, max).Select(ToDto).ToList();
        if (List.Count == 0) _logger.LogError("The built-in drug list is empty or could not be read ({Resource}).", ResourceName);
        return Task.FromResult<IEnumerable<EgyptianMedicineDto>>(found);
    }

    public static EgyptianMedicineDto ToDto(DrugEntry e) => new()
    {
        TradeName = e.Name,
        ArabicName = e.ArabicName,
        ScientificName = e.ScientificName,
        Description = RouteLabel(e.Route),
        DrugClass = e.DrugClass,
        Strength = string.Empty,   // part of the trade name in this list, e.g. "AUGMENTIN 625 MG 10 F.C.TABS."
        PublicPrice = e.Price,
        Manufacturer = e.Manufacturer
    };

    private static string RouteLabel(string route) => route switch
    {
        "ORAL.SOLID" => "Oral (solid)",
        "ORAL.LIQUID" => "Oral (liquid)",
        "EFF" => "Effervescent",
        "INJECTION" => "Injection",
        "TOPICAL" => "Topical",
        "EYE" => "Eye",
        "EAR" => "Ear",
        "MOUTH" => "Mouth",
        "RECTAL" => "Rectal",
        "VAGINAL" => "Vaginal",
        "SPRAY" => "Spray",
        "SOAP" => "Soap",
        _ => string.Empty
    };

    private static DrugIndex Load()
    {
        using var stream = typeof(LocalEgyptianDrugSource).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return new DrugIndex(Array.Empty<DrugEntry>());
        using var unzip = new GZipStream(stream, CompressionMode.Decompress);
        var rows = JsonSerializer.Deserialize<List<Row>>(unzip) ?? new List<Row>();
        return new DrugIndex(rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new DrugEntry(
                r.Name!.Trim(), r.Arabic?.Trim() ?? "", r.Scientific?.Trim() ?? "", r.Manufacturer?.Trim() ?? "",
                r.DrugClass?.Trim() ?? "", r.Route?.Trim() ?? "", r.Price ?? 0m)));
    }

    private sealed class Row
    {
        [JsonPropertyName("commercial_name_en")] public string? Name { get; set; }
        [JsonPropertyName("commercial_name_ar")] public string? Arabic { get; set; }
        [JsonPropertyName("scientific_name")] public string? Scientific { get; set; }
        [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
        [JsonPropertyName("drug_class")] public string? DrugClass { get; set; }
        [JsonPropertyName("route")] public string? Route { get; set; }
        [JsonPropertyName("price_egp")] public decimal? Price { get; set; }
    }
}
