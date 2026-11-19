using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Domain.Entities;
using CivicTrace.Persistence.EntityTypeConfigurations;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Persistence.Context;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Procurement> Procurements => Set<Procurement>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RentalObject> RentalObjects => Set<RentalObject>();
    public DbSet<RentalAuction> RentalAuctions => Set<RentalAuction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly,
            type => type.Namespace == typeof(ProcurementConfiguration).Namespace);
    }
}