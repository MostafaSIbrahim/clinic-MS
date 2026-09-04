using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;

namespace SafyaClinic.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SafyaDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("SafyaClinicDb"),
                sql => sql.MigrationsAssembly(
                    typeof(SafyaDbContext).Assembly.FullName)));
        // Register UnitOfWork
        services.AddScoped<IUnitOfWork, UnitOfWork>();


        return services;
    }
}