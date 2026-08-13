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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsnanDbContext).Assembly);
    }
}
