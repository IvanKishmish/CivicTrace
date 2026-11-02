using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Procurement> Procurements { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<UserProfile> UserProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}