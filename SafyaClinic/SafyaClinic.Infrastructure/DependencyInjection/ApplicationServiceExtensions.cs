using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Services;

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

        return services;
    }
}