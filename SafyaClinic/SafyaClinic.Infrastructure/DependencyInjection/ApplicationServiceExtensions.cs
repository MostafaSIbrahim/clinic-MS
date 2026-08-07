using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Options;
using SafyaClinic.Application.Services;
using SafyaClinic.Application.Services.EgyptianDrugIndex;

namespace SafyaClinic.Application.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IPatientRecordService, PatientRecordService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<INutritionService, NutritionService>();
        services.AddScoped<IPatientSourceService, PatientSourceService>();
        services.AddScoped<IClinicService, ClinicService>();

        // ── Egyptian Drug Index (medication autocomplete) ─────────
        services.AddMemoryCache();
        services.Configure<EgyptianDrugIndexOptions>(
            configuration.GetSection(EgyptianDrugIndexOptions.SectionName));

        var drugIndexProvider = configuration[$"{EgyptianDrugIndexOptions.SectionName}:Provider"] ?? "Local";
        if (string.Equals(drugIndexProvider, "DwaPrices", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IEgyptianDrugSource, DwaPricesEgyptianDrugSource>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<EgyptianDrugIndexOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 5);
            });
        }
        else
        {
            services.AddSingleton<IEgyptianDrugSource, LocalEgyptianDrugSource>();
        }

        services.AddScoped<IEgyptianDrugService, EgyptianDrugService>();

        return services;
    }
}