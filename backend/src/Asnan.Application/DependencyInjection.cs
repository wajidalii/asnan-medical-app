using System.Reflection;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Application.Doctors;
using Asnan.Application.Otps;
using Asnan.Application.Specialties;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asnan.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.AddScoped<IOtpService, OtpService>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ISignupService, SignupService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddScoped<ISpecialtyService, SpecialtyService>();
        services.AddScoped<IDoctorService, DoctorService>();
        services.AddScoped<IDoctorSearchService, DoctorSearchService>();

        services.AddScoped<IDoctorScheduleService, DoctorScheduleService>();
        services.AddScoped<IAvailabilityExceptionService, AvailabilityExceptionService>();
        services.AddScoped<IAvailabilityComputationService, AvailabilityComputationService>();

        services.Configure<HoldOptions>(configuration.GetSection(HoldOptions.SectionName));
        services.AddScoped<IAppointmentHoldService, AppointmentHoldService>();

        return services;
    }
}
