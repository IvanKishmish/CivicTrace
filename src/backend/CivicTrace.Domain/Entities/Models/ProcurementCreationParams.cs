using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;

namespace CivicTrace.Domain.Entities.Models;

public sealed record ProcurementCreationParams(
    string ExternalId,
    string? ApiId,
    string? PlanExternalId,
    string Title,
    ProcurementKind Kind,
    CpvClassification Cpv,
    ProcurementMethod Method,
    Guid BuyerId,
    Guid? ContractorId,
    MoneyAmount? ExpectedAmount,
    MoneyAmount? ContractAmount,
    ProcurementStatus Status,
    string SourceStatus,
    ProcurementPlace Place,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    NonCompetitiveBasis? NonCompetitiveBasis,
    bool BidsListComplete,
    string SourceUrl,
    DateOnly? VerifiedAt,
    Guid? VerifiedByUserId);