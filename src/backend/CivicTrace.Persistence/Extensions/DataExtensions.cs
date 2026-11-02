using CivicTrace.Persistence.Context;
using CivicTrace.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence.Extensions;

public static class DataExtensions
{
    /// <summary>Застосовує міграції обох контекстів: AppDbContext і ApplicationIdentityDbContext.</summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();

        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();

        await appDb.Database.MigrateAsync(ct);
        await identityDb.Database.MigrateAsync(ct);
    }
}