using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P3: find a patient by any words of name, ID or phone; one match opens the patient; duplicates link to the existing one.</summary>
public class PatientSearchTests
{
    private static async Task<SafyaDbContext> PatientsAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"patient-search-{Guid.NewGuid()}").Options);
        db.Patients.AddRange(
            new Patient { FirstName = "Mona", LastName = "Adel", Phones = { new PatientPhone { PhoneNumber = "01001234567", IsPrimary = true } } },
            new Patient { FirstName = "Mona", LastName = "Zaki", Phones = { new PatientPhone { PhoneNumber = "01119876543", IsPrimary = true } } },
            new Patient { FirstName = "Karim", LastName = "Adel", Phones = { new PatientPhone { PhoneNumber = "01225550000", IsPrimary = true } } });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<string[]> FindAsync(SafyaDbContext db, string search)
    {
        db.ChangeTracker.Clear();
        var result = await new PatientService(new UnitOfWork(db)).SearchPatientsAsync(new PaginationRequest { Search = search });
        return result.Data!.Items.Select(p => p.FullName).OrderBy(n => n).ToArray();
    }

    [Theory]
    [InlineData("mona adel", "Mona Adel")]
    [InlineData("Adel 0100", "Mona Adel")]
    [InlineData("+20 100 123 4567", "Mona Adel")]
    [InlineData("0100 123 4567", "Mona Adel")]
    [InlineData("01119876543", "Mona Zaki")]
    public async Task Every_word_must_match_and_phones_match_in_any_format(string search, string expected)
    {
        await using var db = await PatientsAsync();

        Assert.Equal(new[] { expected }, await FindAsync(db, search));
    }

    [Fact]
    public async Task One_word_still_finds_everyone_it_matches_and_unknown_words_find_nobody()
    {
        await using var db = await PatientsAsync();

        Assert.Equal(new[] { "Mona Adel", "Mona Zaki" }, await FindAsync(db, "mona"));
        Assert.Empty(await FindAsync(db, "mona karim"));
    }

    [Fact]
    public void The_existing_patient_is_read_from_the_duplicate_phone_message()
    {
        var errors = new[] { "This phone number is already the primary phone of Mona Adel (patient #42). Open that patient instead of registering a new one." };

        Assert.Equal(42, DuplicatePatient.FindId(errors));
        Assert.Null(DuplicatePatient.FindId(new[] { "Enter the patient's phone number." }));
    }

    // ── One match opens the patient ─────────────────────────────

    public class OneMatch : DispatchProxy
    {
        public int Matches { get; set; } = 1;

        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IPatientService.SearchPatientsAsync) => Task.FromResult(ServiceResult<PagedResult<PatientSummaryDto>>.Success(
                new PagedResult<PatientSummaryDto>
                {
                    Items = Enumerable.Range(1, Matches).Select(i => new PatientSummaryDto { Id = 41 + i, FullName = $"Patient {i}" }).ToList(),
                    TotalCount = Matches,
                })),
            _ => throw new NotSupportedException(method.Name),
        };
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static PatientsController Controller(int matches)
    {
        var service = DispatchProxy.Create<IPatientService, OneMatch>();
        ((OneMatch)(object)service).Matches = matches;
        var http = new DefaultHttpContext();
        return new PatientsController(service, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempData()),
        };
    }

    [Fact]
    public async Task A_search_with_one_match_opens_that_patient()
    {
        var result = await Controller(matches: 1).Index(new PaginationRequest { Search = "0100 123 4567" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(42, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task A_search_with_several_matches_shows_the_list()
    {
        Assert.IsType<ViewResult>(await Controller(matches: 2).Index(new PaginationRequest { Search = "mona" }));
    }
}
