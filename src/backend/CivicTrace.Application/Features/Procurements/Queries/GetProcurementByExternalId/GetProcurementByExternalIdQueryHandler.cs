using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Procurements.Common;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Procurements.Queries.GetProcurementByExternalId;

public sealed class GetProcurementByExternalIdQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetProcurementByExternalIdQuery, ErrorOr<ProcurementResponse>>
{
    public async ValueTask<ErrorOr<ProcurementResponse>> Handle(
        GetProcurementByExternalIdQuery query,
        CancellationToken ct = default)
    {
        var procurement = await dbContext.Procurements
            .AsNoTracking()
            .Where(p => p.ExternalId == query.ExternalId)
            .IncludeFull()
            .FirstOrDefaultAsync(ct);

        if (procurement is null)
            return Error.NotFound("Procurement.NotFound", "Procurement not found.");

        var verifierNames = await dbContext.LoadVerifierNamesAsync([procurement], ct);

        return ProcurementMapper.ToResponse(procurement, verifierNames.VerifierName(procurement));
    }
}