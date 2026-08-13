using Microsoft.EntityFrameworkCore;

namespace Asnan.Infrastructure.Persistence;

public class AsnanDbContext : DbContext
{
    public AsnanDbContext(DbContextOptions<AsnanDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsnanDbContext).Assembly);
    }
}
