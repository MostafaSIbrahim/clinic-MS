using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.DTOs.Medicine;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Options;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

public class EgyptianDrugService : IEgyptianDrugService
{
    private const string CacheKeyPrefix = "EgyptianDrugSearch:";

    private readonly IEgyptianDrugSource _source;
    private readonly IMemoryCache _cache;
    private readonly EgyptianDrugIndexOptions _options;
    private readonly ILogger<EgyptianDrugService> _logger;

    public EgyptianDrugService(
        IEgyptianDrugSource source,
        IMemoryCache cache,
        IOptions<EgyptianDrugIndexOptions> options,
        ILogger<EgyptianDrugService> logger)
    {
        _source = source;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<EgyptianMedicineDto>> SearchMedicinesAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < _options.MinQueryLength)
            return Enumerable.Empty<EgyptianMedicineDto>();

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var cacheKey = $"{CacheKeyPrefix}{normalizedQuery}";

        if (_cache.TryGetValue(cacheKey, out IEnumerable<EgyptianMedicineDto>? cached) && cached is not null)
            return cached;

        IEnumerable<EgyptianMedicineDto> results;
        try
        {
            results = await _source.FetchAsync(normalizedQuery, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // caller (e.g. a cancelled HTTP request) should see this, not an empty result
        }
        catch (Exception ex)
        {
            // Autocomplete should degrade to "no suggestions", never break the
            // prescription form just because the drug index is unreachable.
            _logger.LogWarning(ex, "Egyptian drug index lookup failed for query '{Query}'", query);
            results = Enumerable.Empty<EgyptianMedicineDto>();
        }

        var limited = results.Take(_options.MaxResults).ToList();

        // Cache individual search terms to reduce load on the source
        // (especially important once Provider == "Http").
        _cache.Set(cacheKey, (IEnumerable<EgyptianMedicineDto>)limited,
            TimeSpan.FromMinutes(_options.CacheMinutes));

        return limited;
    }
}
