using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>M4c: a new package needs a name and quantities above zero; a valid one is saved with its items.</summary>
public class PackageTests
{
    private static async Task<(NutritionService Service, SafyaDbContext Db, int Injection)> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"package-{Guid.NewGuid()}").Options);
        var injection = new InjectionType { InjectionName = "B12", Unit = "ml" };
        db.Add(injection);
        await db.SaveChangesAsync();
        return (new NutritionService(new UnitOfWork(db)), db, injection.Id);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("   ", 1)]
    [InlineData("Monthly", 0)]
    [InlineData("Monthly", -2)]
    public async Task A_package_needs_a_name_and_positive_quantities(string name, decimal quantity)
    {
        var (service, db, injection) = await WorldAsync();

        var result = await service.CreatePackageAsync(new CreateNutritionPackageDto
        {
            PackageName = name, BasePrice = 1000m,
            Items = { new CreatePackageItemDto { InjectionId = injection, WeekNumber = 1, Quantity = quantity } },
        }, 1);

        Assert.False(result.IsSuccess);
        Assert.Empty(await db.NutritionPackages.ToListAsync());
    }

    [Fact]
    public async Task A_valid_package_is_saved_with_its_items()
    {
        var (service, db, injection) = await WorldAsync();

        var result = await service.CreatePackageAsync(new CreateNutritionPackageDto
        {
            PackageName = " Monthly ", BasePrice = 1000m,
            Items = { new CreatePackageItemDto { InjectionId = injection, WeekNumber = 2, Quantity = 1.5m, Unit = "ml" } },
        }, 1);

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
        var package = await db.NutritionPackages.SingleAsync();
        Assert.Equal("Monthly", package.PackageName);
        var item = await db.PackageItems.SingleAsync();
        Assert.Equal((2, 1.5m), (item.WeekNumber, item.Quantity));
    }
}
