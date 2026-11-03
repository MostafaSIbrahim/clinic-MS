using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Security;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Controllers.Api
{
    // JSON-only endpoint consumed by the medication typeahead on the
    // prescription form (see wwwroot/js/medicine-autocomplete.js).
    [ApiController]
    [Route("api/medicines")]
    [RequirePermission(Permissions.Medicines.Search)]
    public class MedicinesController : ControllerBase
    {
        private readonly IEgyptianDrugService _drugService;

        public MedicinesController(IEgyptianDrugService drugService)
        {
            _drugService = drugService;
        }

        // GET /api/medicines/search?query=aug
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
        {
            var results = await _drugService.SearchMedicinesAsync(query ?? string.Empty, cancellationToken);
            return Ok(results);
        }
    }
}
