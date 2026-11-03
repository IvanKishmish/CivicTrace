using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Common.Interfaces;

public interface IAppDbContext : IDisposable, IAsyncDisposable
{
    DbSet<Procurement> Procurements { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<UserProfile> UserProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}