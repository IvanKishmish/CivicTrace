using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Procurements.Common;

public static class ProcurementQueryExtensions
{
    /// <summary>Підвантажує все, що потрібно для ProcurementResponse.</summary>
    public static IQueryable<Procurement> IncludeFull(this IQueryable<Procurement> query)
        => query
            .Include(p => p.Buyer)
            .Include(p => p.Contractor)
            .Include(p => p.Events)
            .Include(p => p.Bids).ThenInclude(b => b.Bidder)
            .Include(p => p.FundingSources)
            .Include(p => p.Notes)
            .Include(p => p.DataGaps)
            .AsSplitQuery();

    /// <summary>Імена користувачів, які позначили закупівлі перевіреними.</summary>
    public static async Task<Dictionary<Guid, string>> LoadVerifierNamesAsync(
        this IAppDbContext dbContext,
        IEnumerable<Procurement> procurements,
        CancellationToken ct)
    {
        var ids = procurements
            .Where(p => p.VerifiedByUserId is not null)
            .Select(p => p.VerifiedByUserId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await dbContext.UserProfiles
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    public static string? VerifierName(this Dictionary<Guid, string> names, Procurement procurement)
        => procurement.VerifiedByUserId is { } id && names.TryGetValue(id, out var name) ? name : null;
}