using CivicTrace.Application.Features.Procurements.Common;
using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Procurements.Queries.GetProcurementByExternalId;

public sealed record GetProcurementByExternalIdQuery(string ExternalId) : IQuery<ErrorOr<ProcurementResponse>>;