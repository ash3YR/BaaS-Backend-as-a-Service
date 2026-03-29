using BaaS.Models;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<ProvisionedTableRecord> ProvisionedTables => Set<ProvisionedTableRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(user => user.Email)
            .HasMaxLength(320);

        modelBuilder.Entity<AppUser>()
            .HasMany(user => user.ProvisionedTables)
            .WithOne(record => record.AppUser)
            .HasForeignKey(record => record.AppUserId);

        modelBuilder.Entity<ProvisionedTableRecord>()
            .HasIndex(record => new { record.AppUserId, record.TableName })
            .IsUnique();
    }
}
