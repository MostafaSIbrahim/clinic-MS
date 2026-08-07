using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.DTOs.Medicine;
using SafyaClinic.Application.Options;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

// Talks to DwaPrices (https://dwaprices.com) — "دليل دواء مصر".
//
// Documented endpoint (as of their public API docs, July 2026):
//   GET https://dwaprices.com/sample-api.php
//     ?search={term}      free-text search (English/Arabic name or barcode)
//     &limit={n}          max results per page (their cap: 100)
//     &page={n}           pagination
//     &company={name}     optional manufacturer filter (unused here)
//
// That "sample-api.php" URL is explicitly documented as their FREE PREVIEW
// endpoint for developers to test against before subscribing — it is not
// necessarily the production endpoint you'll be given once you pay for a
// plan. When DwaPrices gives you real production credentials/URL, just
// update EgyptianDrugIndex:BaseUrl/SearchPath/ApiKey in configuration;
// nothing here should need to change as long as the response envelope
// stays the same shape (it's the same product, just gated differently).
//
// Response envelope (real, observed):
//   {
//     "status": "success",
//     "data": [
//       {
//         "id": 24483,
//         "name": "panadol sinus relief pe 24 tablets",
//         "arabic": "بانادول ساينس ريليف بي 24 قرص",
//         "oldprice": "22.8",
//         "price": "39",
//         "active": "paracetamol(acetaminophen)+phenylepherine",
//         "company": "alexandria > glaxo smithkline",
//         "description": "cold drugs",          <- therapeutic class, NOT dosage form
//         "units": 2,
//         "dosage_form": "tablet",               <- this is the dosage form
//         "barcode": "6222010610787",
//         "imported": "imported",
//         "Date_updated": null,
//         "sold_times": 708
//       }
//     ],
//     "pagination": { "total": 4, "per_page": 1, "current_page": 1, "last_page": 4 },
//     "meta": { "search_term": "panadol", "company_filter": null, "request_time": "..." }
//   }
//
// Notes / known gaps in their schema vs. our EgyptianMedicineDto:
//   - price/oldprice are STRINGS and can be "" (empty) — parsed defensively below.
//   - There's no discrete "strength" field (e.g. "500mg"); it's often embedded
//     in `name` (e.g. "panadol advance 500 mg 48 tablets") but not reliably
//     parseable, so EgyptianMedicineDto.Strength is left blank for this
//     provider. The autocomplete UI already tolerates a blank Strength.
//   - Their own "description" field is a therapeutic category (e.g. "analgesic"),
//     which is NOT what our DTO's Description means (dosage form) — mapped
//     from `dosage_form` instead, not from their `description`.
public class DwaPricesEgyptianDrugSource : IEgyptianDrugSource
{
    private readonly HttpClient _httpClient;
    private readonly EgyptianDrugIndexOptions _options;
    private readonly ILogger<DwaPricesEgyptianDrugSource> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DwaPricesEgyptianDrugSource(
        HttpClient httpClient,
        IOptions<EgyptianDrugIndexOptions> options,
        ILogger<DwaPricesEgyptianDrugSource> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<EgyptianMedicineDto>> FetchAsync(
        string normalizedQuery, CancellationToken cancellationToken)
    {
        var path =
            $"{_options.SearchPath.TrimStart('/')}" +
            $"?{_options.QueryParameterName}={Uri.EscapeDataString(normalizedQuery)}" +
            $"&limit={Math.Clamp(_options.MaxResults, 1, 100)}";

        // Only sent if you've configured a key (the free sample tier doesn't need one).
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove(_options.ApiKeyHeaderName);
            _httpClient.DefaultRequestHeaders.Add(_options.ApiKeyHeaderName, _options.ApiKey);
        }

        using var response = await _httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "DwaPrices API returned {StatusCode} for query '{Query}'",
                response.StatusCode, normalizedQuery);
            return Enumerable.Empty<EgyptianMedicineDto>();
        }

        var envelope = await response.Content.ReadFromJsonAsync<DwaPricesResponse>(
            JsonOptions, cancellationToken);

        if (envelope is null || !string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase)
            || envelope.Data is null)
        {
            return Enumerable.Empty<EgyptianMedicineDto>();
        }

        return envelope.Data.Select(MapToDto);
    }

    private static EgyptianMedicineDto MapToDto(DwaPricesMedicineItem item) => new()
    {
        TradeName = (item.Name ?? string.Empty).Trim(),
        ScientificName = (item.Active ?? string.Empty).Trim(),
        Description = (item.DosageForm ?? string.Empty).Trim(),
        Strength = string.Empty, // not provided as a discrete field by this API
        PublicPrice = ParsePrice(item.Price),
        Manufacturer = (item.Company ?? string.Empty).Trim()
    };

    private static decimal ParsePrice(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;

    // ── Wire types matching DwaPrices' actual JSON shape ──────────────

    private class DwaPricesResponse
    {
        public string? Status { get; set; }
        public List<DwaPricesMedicineItem>? Data { get; set; }
    }

    private class DwaPricesMedicineItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Arabic { get; set; }
        public string? Price { get; set; }
        public string? Oldprice { get; set; }
        public string? Active { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
        public int? Units { get; set; }

        [JsonPropertyName("dosage_form")]
        public string? DosageForm { get; set; }

        public string? Barcode { get; set; }
        public string? Imported { get; set; }
    }
}
