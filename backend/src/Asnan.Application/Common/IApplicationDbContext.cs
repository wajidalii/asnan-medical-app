using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Asnan.Application.Common;

/// <summary>
/// Narrow view of <c>AsnanDbContext</c> that the Application layer is allowed
/// to depend on — keeps Application testable/EF-Core-provider-agnostic
/// without duplicating a repository per entity. Extended as new entities are
/// needed by use-case services; implemented by <c>AsnanDbContext</c>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Otp> Otps { get; }

    DbSet<User> Users { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<SignupToken> SignupTokens { get; }

    DbSet<UserSession> UserSessions { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Specialty> Specialties { get; }

    DbSet<DoctorProfile> DoctorProfiles { get; }

    DbSet<DoctorSpecialty> DoctorSpecialties { get; }

    DbSet<DoctorSchedule> DoctorSchedules { get; }

    DbSet<DoctorAvailabilityException> DoctorAvailabilityExceptions { get; }

    DbSet<AppointmentHold> AppointmentHolds { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<AppointmentStatusHistory> AppointmentStatusHistories { get; }

    DbSet<PaymentTransaction> PaymentTransactions { get; }

    DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents { get; }

    DbSet<ChatConversation> ChatConversations { get; }

    DbSet<ChatParticipant> ChatParticipants { get; }

    DbSet<Refund> Refunds { get; }

    DbSet<Reminder> Reminders { get; }

    DbSet<ChatMessage> ChatMessages { get; }

    DbSet<MessageReadStatus> MessageReadStatuses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Needed to recover a tracked entity's state after a
    /// DbUpdateConcurrencyException — see RefreshTokenService.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
