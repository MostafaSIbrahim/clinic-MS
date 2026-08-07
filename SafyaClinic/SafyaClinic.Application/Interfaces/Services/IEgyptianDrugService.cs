using SafyaClinic.Application.DTOs.Medicine;

namespace SafyaClinic.Application.Interfaces.Services;

// Powers the medication typeahead/autocomplete on the prescription form.
public interface IEgyptianDrugService
{
    Task<IEnumerable<EgyptianMedicineDto>> SearchMedicinesAsync(
        string query, CancellationToken cancellationToken = default);
}
