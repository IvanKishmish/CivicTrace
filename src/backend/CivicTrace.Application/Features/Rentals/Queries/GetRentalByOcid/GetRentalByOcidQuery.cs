using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Rentals.Common;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Rentals.Queries.GetRentalByOcid;

public sealed record GetRentalByOcidQuery(string Ocid) : IQuery<ErrorOr<RentalObjectResponse>>;

public sealed class GetRentalByOcidQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRentalByOcidQuery, ErrorOr<RentalObjectResponse>>
{
    public async ValueTask<ErrorOr<RentalObjectResponse>> Handle(GetRentalByOcidQuery query, CancellationToken ct = default)
    {
        var obj = await dbContext.RentalObjects.AsNoTracking().Include(r => r.Auctions)
            .FirstOrDefaultAsync(r => r.Ocid == query.Ocid, ct);

        if (obj is null) return Error.NotFound("Rental.NotFound", "Rental object not found.");

        var allAuctions = await dbContext.RentalAuctions.AsNoTracking().ToListAsync(ct);
        return RentalMapper.ToResponse(obj, RentalMapper.ComputeMedianRate(allAuctions), RiskRulesConfig.Load());
    }
}