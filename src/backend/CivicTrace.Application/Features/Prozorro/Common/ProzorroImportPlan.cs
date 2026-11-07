using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;

namespace CivicTrace.Application.Features.Prozorro.Common;

public sealed record ImportedOrganization(string Name, string Edrpou, string? Address);

public sealed record ImportedBid(string BidderEdrpou, decimal Amount, bool? VatIncluded, BidOutcome? Outcome);

public sealed record ImportedEvent(DateOnly? Date, EventStage Stage, string Title, string? DateNote, string SourceUrl);

/// <summary>Результат чистого перетворення відповіді Prozorro у наші поняття; ще нічого не збережено.</summary>
public sealed record ProzorroImportPlan(
    string ExternalId,
    string ApiId,
    string? PlanExternalId,
    string Title,
    ProcurementKind Kind,
    CpvClassification Cpv,
    ProcurementMethod Method,
    ImportedOrganization Buyer,
    ImportedOrganization? Contractor,
    IReadOnlyList<ImportedOrganization> Bidders,
    MoneyAmount? ExpectedAmount,
    MoneyAmount? ContractAmount,
    ProcurementStatus Status,
    string SourceStatus,
    ProcurementPlace Place,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    NonCompetitiveBasis? NonCompetitiveBasis,
    bool BidsListComplete,
    IReadOnlyList<ImportedBid> Bids,
    IReadOnlyList<ImportedEvent> Events,
    IReadOnlyList<FundingSourceCreationParams> Funding,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> MissingData,
    IReadOnlyList<string> NeedsCheck,
    IReadOnlyList<string> Warnings,
    string SourceUrl);