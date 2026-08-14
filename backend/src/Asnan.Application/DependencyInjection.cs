using System.Reflection;
using Asnan.Application.Appointments;
using Asnan.Application.Auth;
using Asnan.Application.Availability;
using Asnan.Application.Chat;
using Asnan.Application.Doctors;
using Asnan.Application.Notifications;
using Asnan.Application.Otps;
using Asnan.Application.Payments;
using Asnan.Application.Reminders;
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

        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRefundService, RefundService>();

        services.Configure<CancellationPolicyOptions>(configuration.GetSection(CancellationPolicyOptions.SectionName));
        services.AddScoped<IAppointmentService, AppointmentService>();

        services.Configure<ReminderOptions>(configuration.GetSection(ReminderOptions.SectionName));
        services.AddScoped<IReminderSchedulingService, ReminderSchedulingService>();

        services.AddScoped<IChatService, ChatService>();

        services.AddScoped<INotificationDeviceService, NotificationDeviceService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationDispatchService, NotificationDispatchService>();

        return services;
    }
}
