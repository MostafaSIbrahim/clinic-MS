using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Application.Security;
using SafyaClinic.Infrastructure.Security;

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
        services.AddScoped<IPermissionStore, PermissionStore>();   // F4b
        services.AddScoped<IPermissionGroupService, PermissionGroupService>();   // F4d


        return services;
    }
}