using System.Reflection;
using Asnan.Application.Auth;
using Asnan.Application.Otps;
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

        return services;
    }
}
