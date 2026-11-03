namespace CivicTrace.Application.Features.Procurements.Common;

/// <summary>
/// Форма відповіді збігається з data/samples/procurements.seed.json: той самий набір полів і ті самі коди
/// (status = contract_signed / not_held, stage = snake_case тощо). Завдяки цьому фронтенд перемикається
/// зі статичного файлу на API без змін у mapper.js.
/// </summary>
public sealed record ProcurementResponse(
    string Id,
    string? ApiId,
    string? PlanId,
    string Title,
    string Kind,
    string CpvCode,
    string CpvName,
    string ProcurementType,
    string Buyer,
    string BuyerEdrpou,
    string? BuyerAddress,
    string? Contractor,
    string? ContractorEdrpou,
    decimal? ExpectedAmount,
    decimal? ContractAmount,
    string Currency,
    bool? ExpectedVatIncluded,
    bool? ContractVatIncluded,
    string Status,
    string StatusLabel,
    string SourceStatus,
    string ExecutionStatus,
    string? Address,
    string PlaceType,
    double? Latitude,
    double? Longitude,
    bool HasCoordinates,
    string? CoordinatesStatus,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyList<FundingResponse> Funding,
    NonCompetitiveBasisResponse? NonCompetitiveBasis,
    bool BidsListComplete,
    IReadOnlyList<BidResponse> Bids,
    IReadOnlyList<EventResponse> Events,
    string SourceUrl,
    IReadOnlyList<NoteResponse> Notes,
    IReadOnlyList<string> MissingData,
    IReadOnlyList<string> NeedsCheck,
    DateOnly? VerifiedAt,
    string? VerifiedBy);

public sealed record FundingResponse(
    string Source,
    string? ExpenditureCode,
    string? Territory,
    decimal Amount);

public sealed record NonCompetitiveBasisResponse(
    string LegalBasis,
    string Justification,
    string SourceUrl);

public sealed record BidResponse(
    string Bidder,
    string BidderEdrpou,
    decimal Amount,
    bool? VatIncluded,
    string? Outcome);

public sealed record EventResponse(
    DateOnly? Date,
    string Stage,
    string Title,
    string? DateNote,
    string SourceUrl);

public sealed record NoteResponse(
    string Text,
    string SourceUrl);