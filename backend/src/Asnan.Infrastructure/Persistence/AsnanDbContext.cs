using Asnan.Application.Common;
using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asnan.Infrastructure.Persistence;

public class AsnanDbContext : DbContext, IApplicationDbContext
{
    public AsnanDbContext(DbContextOptions<AsnanDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Otp> Otps => Set<Otp>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SignupToken> SignupTokens => Set<SignupToken>();

    public DbSet<Specialty> Specialties => Set<Specialty>();

    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();

    public DbSet<DoctorSpecialty> DoctorSpecialties => Set<DoctorSpecialty>();

    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();

    public DbSet<DoctorAvailabilityException> DoctorAvailabilityExceptions => Set<DoctorAvailabilityException>();

    public DbSet<AppointmentHold> AppointmentHolds => Set<AppointmentHold>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<AppointmentStatusHistory> AppointmentStatusHistories => Set<AppointmentStatusHistory>();

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();

    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<MessageReadStatus> MessageReadStatuses => Set<MessageReadStatus>();

    public DbSet<NotificationDevice> NotificationDevices => Set<NotificationDevice>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsnanDbContext).Assembly);
    }
}
