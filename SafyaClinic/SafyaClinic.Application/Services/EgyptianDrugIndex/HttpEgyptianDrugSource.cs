using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.DTOs.Medicine;
using SafyaClinic.Application.Options;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

// Calls a real, externally-hosted Egyptian drug-index REST API.
//
// NOT CURRENTLY WIRED UP — the app is now configured to use
// DwaPricesEgyptianDrugSource (see appsettings.json EgyptianDrugIndex:Provider).
// This generic version assumes the API returns a bare JSON array whose
// objects already match EgyptianMedicineDto's property names 1:1, which is
// NOT how DwaPrices' API is shaped (it wraps results in a {status,data:[...]}
// envelope with different field names — see DwaPricesEgyptianDrugSource).
// Kept here as a starting point if you ever integrate a *different* provider
// whose response really does look like a flat array of EgyptianMedicineDto.
public class HttpEgyptianDrugSource : IEgyptianDrugSource
{
    private readonly HttpClient _httpClient;
    private readonly EgyptianDrugIndexOptions _options;
    private readonly ILogger<HttpEgyptianDrugSource> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpEgyptianDrugSource(
        HttpClient httpClient,
        IOptions<EgyptianDrugIndexOptions> options,
        ILogger<HttpEgyptianDrugSource> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<EgyptianMedicineDto>> FetchAsync(
        string normalizedQuery, CancellationToken cancellationToken)
    {
        var path = $"{_options.SearchPath.TrimStart('/')}" +
                   $"?{_options.QueryParameterName}={Uri.EscapeDataString(normalizedQuery)}";

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Remove(_options.ApiKeyHeaderName);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Add(_options.ApiKeyHeaderName, _options.ApiKey);

        using var response = await _httpClient.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Egyptian drug index API returned {StatusCode} for query '{Query}'",
                response.StatusCode, normalizedQuery);
            return Enumerable.Empty<EgyptianMedicineDto>();
        }

        var results = await response.Content.ReadFromJsonAsync<IEnumerable<EgyptianMedicineDto>>(
            JsonOptions, cancellationToken);

        return results ?? Enumerable.Empty<EgyptianMedicineDto>();
    }
}
