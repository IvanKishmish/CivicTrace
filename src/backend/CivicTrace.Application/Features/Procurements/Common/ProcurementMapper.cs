using CivicTrace.Domain.Entities;
using CivicTrace.Domain.Enums;

namespace CivicTrace.Application.Features.Procurements.Common;

/// <summary>
/// Перетворює доменні сутності на відповіді API й назад розбирає коди фільтрів.
/// Усі «людські» підписи (kind, procurementType, statusLabel) збігаються з сид-файлом.
/// </summary>
public static class ProcurementMapper
{
    public static ProcurementResponse ToResponse(Procurement p, string? verifiedBy)
        => ToResponse(p, verifiedBy, p.Buyer, p.Contractor, b => b.Bidder);

    /// <summary>
    /// Той самий результат, але організації передаються явно: потрібно, коли закупівля ще не збережена
    /// й навігаційні властивості порожні (імпорт з Prozorro, режим dry run).
    /// </summary>
    public static ProcurementResponse ToResponse(
        Procurement p,
        string? verifiedBy,
        Organization buyer,
        Organization? contractor,
        Func<Bid, Organization> bidderOf)
    {
        var missing = p.DataGaps.Where(g => g.Kind == DataGapKind.Missing).OrderBy(g => g.Id).Select(g => g.Text).ToList();
        var needsCheck = p.DataGaps.Where(g => g.Kind == DataGapKind.NeedsCheck).OrderBy(g => g.Id).Select(g => g.Text).ToList();

        return new ProcurementResponse(
            Id: p.ExternalId,
            ApiId: p.ApiId,
            PlanId: p.PlanExternalId,
            Title: p.Title,
            Kind: KindLabel(p.Kind),
            CpvCode: p.Cpv.Code,
            CpvName: p.Cpv.Name,
            ProcurementType: MethodLabel(p.Method),
            Buyer: buyer.Name,
            BuyerEdrpou: buyer.Edrpou,
            BuyerAddress: buyer.Address,
            Contractor: contractor?.Name,
            ContractorEdrpou: contractor?.Edrpou,
            ExpectedAmount: p.ExpectedAmount?.Value,
            ContractAmount: p.ContractAmount?.Value,
            Currency: p.ExpectedAmount?.Currency ?? p.ContractAmount?.Currency ?? "UAH",
            ExpectedVatIncluded: p.ExpectedAmount?.VatIncluded,
            ContractVatIncluded: p.ContractAmount?.VatIncluded,
            Status: StatusCode(p.Status),
            StatusLabel: StatusLabel(p.Status),
            SourceStatus: p.SourceStatus,
            ExecutionStatus: ExecutionCode(p.ExecutionStatus),
            Address: p.Place.Address,
            PlaceType: PlaceTypeCode(p.Place.Type),
            Latitude: p.Place.Latitude,
            Longitude: p.Place.Longitude,
            HasCoordinates: p.Place.Latitude is not null && p.Place.Longitude is not null,
            CoordinatesStatus: p.Place.CoordinatesStatus is { } cs ? CoordinatesStatusCode(cs) : null,
            PeriodStart: p.PeriodStart,
            PeriodEnd: p.PeriodEnd,
            Funding: p.FundingSources
                .OrderBy(f => f.Id)
                .Select(f => new FundingResponse(f.Source, f.ExpenditureCode, f.Territory, f.Amount))
                .ToList(),
            NonCompetitiveBasis: p.NonCompetitiveBasis is null
                ? null
                : new NonCompetitiveBasisResponse(
                    p.NonCompetitiveBasis.LegalBasis,
                    p.NonCompetitiveBasis.Justification,
                    p.NonCompetitiveBasis.SourceUrl),
            BidsListComplete: p.BidsListComplete,
            Bids: p.Bids
                .OrderBy(b => b.Id)
                .Select(b => new BidResponse(
                    bidderOf(b).Name,
                    bidderOf(b).Edrpou,
                    b.Amount.Value,
                    b.Amount.VatIncluded,
                    b.Outcome is { } o ? OutcomeCode(o) : null))
                .ToList(),
            Events: p.Events
                .OrderBy(e => e.SortOrder)
                .Select(e => new EventResponse(e.Date, StageCode(e.Stage), e.Title, e.DateNote, e.SourceUrl))
                .ToList(),
            SourceUrl: p.SourceUrl,
            Notes: p.Notes
                .OrderBy(n => n.Id)
                .Select(n => new NoteResponse(n.Text, n.SourceUrl))
                .ToList(),
            MissingData: missing,
            NeedsCheck: needsCheck,
            VerifiedAt: p.VerifiedAt,
            VerifiedBy: verifiedBy);
    }

    // ---------- enum -> відповідь

    public static string KindLabel(ProcurementKind kind) => kind switch
    {
        ProcurementKind.Goods => "Товари",
        ProcurementKind.Services => "Послуги",
        ProcurementKind.Works => "Роботи",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static string MethodLabel(ProcurementMethod method) => method switch
    {
        ProcurementMethod.NoElectronicSystem => "Закупівля без використання електронної системи",
        ProcurementMethod.RequestForQuotations => "Запит (ціни) пропозицій",
        ProcurementMethod.OpenTendersWithSpecifics => "Відкриті торги з особливостями",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
    };

    public static string StatusCode(ProcurementStatus status) => status switch
    {
        ProcurementStatus.ContractSigned => "contract_signed",
        ProcurementStatus.NotHeld => "not_held",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string StatusLabel(ProcurementStatus status) => status switch
    {
        ProcurementStatus.ContractSigned => "Договір укладено",
        ProcurementStatus.NotHeld => "Торги не відбулися",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string ExecutionCode(ExecutionStatus status) => status switch
    {
        ExecutionStatus.NotConfirmed => "not_confirmed",
        ExecutionStatus.NotApplicable => "not_applicable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string PlaceTypeCode(PlaceType type) => type switch
    {
        PlaceType.Unknown => "unknown",
        PlaceType.Work => "work",
        PlaceType.Delivery => "delivery",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static string CoordinatesStatusCode(CoordinatesStatus status) => status switch
    {
        CoordinatesStatus.NeedsCheck => "needs_check",
        CoordinatesStatus.Verified => "verified",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string OutcomeCode(BidOutcome outcome) => outcome switch
    {
        BidOutcome.Winner => "winner",
        BidOutcome.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    public static string StageCode(EventStage stage) => stage switch
    {
        EventStage.Announced => "announced",
        EventStage.ProtocolPublished => "protocol_published",
        EventStage.ClarificationDeadline => "clarification_deadline",
        EventStage.BidsDeadline => "bids_deadline",
        EventStage.BidsOpened => "bids_opened",
        EventStage.BidRejected => "bid_rejected",
        EventStage.AwardNotice => "award_notice",
        EventStage.Reported => "reported",
        EventStage.ContractSigned => "contract_signed",
        EventStage.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };

    // ---------- коди фільтрів -> enum (для параметрів запитів)

    public static readonly string[] StatusCodes = ["contract_signed", "not_held"];
    public static readonly string[] KindCodes = ["goods", "services", "works"];
    public static readonly string[] MethodCodes = ["no_electronic_system", "request_for_quotations", "open_tenders_with_specifics"];

    public static ProcurementStatus? ParseStatus(string? code) => code switch
    {
        "contract_signed" => ProcurementStatus.ContractSigned,
        "not_held" => ProcurementStatus.NotHeld,
        _ => null
    };

    public static ProcurementKind? ParseKind(string? code) => code switch
    {
        "goods" => ProcurementKind.Goods,
        "services" => ProcurementKind.Services,
        "works" => ProcurementKind.Works,
        _ => null
    };

    public static ProcurementMethod? ParseMethod(string? code) => code switch
    {
        "no_electronic_system" => ProcurementMethod.NoElectronicSystem,
        "request_for_quotations" => ProcurementMethod.RequestForQuotations,
        "open_tenders_with_specifics" => ProcurementMethod.OpenTendersWithSpecifics,
        _ => null
    };
}