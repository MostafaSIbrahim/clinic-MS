using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers.Api
{
    [ApiController]
    [Route("api/patients")]
    [Authorize(Policy = "ClinicalStaff")]
    public class PatientsApiController : ControllerBase
    {
        private readonly IPatientService _patientService;

        public PatientsApiController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Ok(Array.Empty<object>());

            var result = await _patientService.SearchPatientsAsync(new PaginationRequest
            {
                Search = query,
                Page = 1,
                PageSize = 20
            });

            if (!result.IsSuccess)
                return Ok(Array.Empty<object>());

            var items = result.Data!.Items.Select(p => new
            {
                id = p.Id,
                fullName = p.FullName,
                primaryPhone = p.PrimaryPhone ?? "",
                age = p.Age,
                nationalId = p.NationalId
            });

            return Ok(items);
        }
    }
}