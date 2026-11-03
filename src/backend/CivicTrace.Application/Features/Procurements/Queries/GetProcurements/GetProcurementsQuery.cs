using CivicTrace.Application.Common;
using CivicTrace.Application.Features.Procurements.Common;
using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Procurements.Queries.GetProcurements;

/// <summary>
/// Фільтри приймають коди: Status = contract_signed | not_held; Kind = goods | services | works;
/// Method = no_electronic_system | request_for_quotations | open_tenders_with_specifics.
/// </summary>
public sealed record GetProcurementsQuery(
    string? Search = null,
    string? Status = null,
    string? Method = null,
    string? Kind = null,
    int? Year = null,
    string? BuyerEdrpou = null,
    bool RepairOnly = false,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<ErrorOr<PagedResponse<ProcurementResponse>>>, IPagedQuery;