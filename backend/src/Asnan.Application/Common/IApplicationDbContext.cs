using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
