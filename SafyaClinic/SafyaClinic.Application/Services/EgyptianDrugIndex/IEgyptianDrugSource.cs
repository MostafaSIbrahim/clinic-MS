using SafyaClinic.Application.DTOs.Medicine;

namespace SafyaClinic.Application.Services.EgyptianDrugIndex;

// Raw, uncached, unvalidated lookup against a single data source.
// EgyptianDrugService wraps whichever implementation is registered with
// the shared caching/validation/error-handling logic.
public interface IEgyptianDrugSource
{
    Task<IEnumerable<EgyptianMedicineDto>> FetchAsync(string normalizedQuery, CancellationToken cancellationToken);
}
