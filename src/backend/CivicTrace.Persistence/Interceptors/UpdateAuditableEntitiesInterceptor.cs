using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CivicTrace.Persistence.Interceptors;

public sealed class UpdateAuditableEntitiesInterceptor(ICurrentUserService currentUserService)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null)
            return;

        var entries = context.ChangeTracker.Entries<Entity>();
        var utcNow = DateTimeOffset.UtcNow;
        // Імпорт та фонові задачі працюють без користувача: як і в QuizArena, використовуємо Guid.Empty
        var userId = currentUserService.UserId ?? Guid.Empty;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(x => x.CreatedAt).CurrentValue = utcNow;
                entry.Property(x => x.CreatedBy).CurrentValue = userId;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.UpdatedAt).CurrentValue = utcNow;
                entry.Property(x => x.UpdatedBy).CurrentValue = userId;
            }
        }
    }
}