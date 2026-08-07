using System.Text.Json;
using Microsoft.Extensions.Logging;
using SafyaClinic.Application.DTOs.Medicine;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

// Default, dependency-free data source: searches a small bundled JSON
// dataset of common Egyptian-market medicines shipped with the app
// (see Data/egyptian-medicines-seed.json). This is deliberately a *starter*
// dataset, not a claim of official/current EDA pricing — swap Provider to
// "Http" in configuration once you've integrated with a licensed real-time
// source, or replace the seed file with a fuller dataset (e.g. the open
// CC0 Egyptian drug database published on GitHub) for a richer offline set.
public class LocalEgyptianDrugSource : IEgyptianDrugSource
{
    private static readonly object LoadLock = new();
    private static IReadOnlyList<EgyptianMedicineDto>? _cachedDataset;

    private readonly ILogger<LocalEgyptianDrugSource> _logger;

    public LocalEgyptianDrugSource(ILogger<LocalEgyptianDrugSource> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<EgyptianMedicineDto>> FetchAsync(
        string normalizedQuery, CancellationToken cancellationToken)
    {
        var dataset = LoadDataset();

        var matches = dataset.Where(m =>
            m.TradeName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
            m.ScientificName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));

        // Trade-name matches first (what a doctor types most often), then by name.
        var ordered = matches
            .OrderByDescending(m => m.TradeName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ThenBy(m => m.TradeName);

        return Task.FromResult<IEnumerable<EgyptianMedicineDto>>(ordered.ToList());
    }

    private IReadOnlyList<EgyptianMedicineDto> LoadDataset()
    {
        if (_cachedDataset is not null) return _cachedDataset;

        lock (LoadLock)
        {
            if (_cachedDataset is not null) return _cachedDataset;

            var path = FindDatasetPath();
            if (path is null)
            {
                _logger.LogError(
                    "Could not locate egyptian-medicines-seed.json under {BaseDirectory}. " +
                    "Make sure SafyaClinic.Application/Data/egyptian-medicines-seed.json is set to " +
                    "CopyToOutputDirectory=PreserveNewest, or set EgyptianDrugIndex:Provider to \"Http\" instead.",
                    AppContext.BaseDirectory);
                _cachedDataset = new List<EgyptianMedicineDto>();
                return _cachedDataset;
            }

            try
            {
                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<EgyptianMedicineDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _cachedDataset = items ?? new List<EgyptianMedicineDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse the local Egyptian medicines dataset at {Path}", path);
                _cachedDataset = new List<EgyptianMedicineDto>();
            }

            return _cachedDataset;
        }
    }

    // The dataset ships as a "None, CopyToOutputDirectory" item in the Application
    // project, which .NET SDK project-to-project references normally propagate
    // into the final host's output directory automatically. These extra
    // candidates are just a safety net in case a given build/publish setup
    // doesn't carry it across (e.g. a custom publish profile).
    private static string? FindDatasetPath()
    {
        const string relative = "Data/egyptian-medicines-seed.json";

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relative),
            Path.Combine(Directory.GetCurrentDirectory(), relative),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "SafyaClinic.Application", relative),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "SafyaClinic.Application", relative)
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }
}
