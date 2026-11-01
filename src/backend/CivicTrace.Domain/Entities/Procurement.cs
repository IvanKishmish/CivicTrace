using ErrorOr;
using CivicTrace.Domain.Common;
using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;

namespace CivicTrace.Domain.Entities;

/// <summary>
/// Кореневий агрегат: одна закупівля з її подіями, пропозиціями, фінансуванням і примітками.
/// </summary>
public sealed class Procurement : Entity
{
    private readonly List<ProcurementEvent> _events = [];
    private readonly List<Bid> _bids = [];
    private readonly List<FundingSource> _fundingSources = [];
    private readonly List<ProcurementNote> _notes = [];
    private readonly List<DataGap> _dataGaps = [];
    
    //Публічний ідентифікатор закупівлі в Prozorro (UA-РРРР-ММ-ДД-NNNNNN-a).
    public string ExternalId { get; private set; } = string.Empty;
    
    //Внутрішній ідентифікатор зі сторінки закупівлі; ймовірно, ключ для API (треба перевірити).
    public string? ApiId { get; private set; }
    
    public string? PlanExternalId { get; private set; } 
    
    public string Title { get; private set; } = string.Empty;
    public ProcurementKind Kind { get; private set; }
    public CpvClassification Cpv { get; private set; } = null!;
    public ProcurementMethod Method { get; private set; }
 
    public Guid BuyerId { get; private set; }
    public Guid? ContractorId { get; private set; }
 
    public MoneyAmount? ExpectedAmount { get; private set; }
    public MoneyAmount? ContractAmount { get; private set; }
 
    public ProcurementStatus Status { get; private set; }
 
    //Статус у Prozorro «як є» («Завершена», «Торги не відбулися»).
    public string SourceStatus { get; private set; } = string.Empty;
 
    //Похідне від Status; у БД не зберігається.
    public ExecutionStatus ExecutionStatus => Status == ProcurementStatus.ContractSigned
        ? ExecutionStatus.NotConfirmed
        : ExecutionStatus.NotApplicable;
 
    public ProcurementPlace Place { get; private set; } = null!;
 
    //Період поставки/виконання за сторінкою закупівлі.
    public DateOnly? PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
 
    public NonCompetitiveBasis? NonCompetitiveBasis { get; private set; }
 
    //true лише якщо сторінка показує повний реєстр пропозицій.
    public bool BidsListComplete { get; private set; }
 
    public string SourceUrl { get; private set; } = string.Empty;
 
    public DateOnly? VerifiedAt { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
 
    //nav props
    public Organization Buyer { get; private set; } = null!;
    public Organization? Contractor { get; private set; }
 
    public IReadOnlyCollection<ProcurementEvent> Events => _events.AsReadOnly();
    public IReadOnlyCollection<Bid> Bids => _bids.AsReadOnly();
    public IReadOnlyCollection<FundingSource> FundingSources => _fundingSources.AsReadOnly();
    public IReadOnlyCollection<ProcurementNote> Notes => _notes.AsReadOnly();
    public IReadOnlyCollection<DataGap> DataGaps => _dataGaps.AsReadOnly();
    
    private Procurement() 
    {} //ef
    
    private Procurement(Guid id, ProcurementCreationParams args) : base(id)
    {
        ExternalId = args.ExternalId.Trim();
        ApiId = string.IsNullOrWhiteSpace(args.ApiId) ? null : args.ApiId.Trim();
        PlanExternalId = string.IsNullOrWhiteSpace(args.PlanExternalId) ? null : args.PlanExternalId.Trim();
        Title = args.Title.Trim();
        Kind = args.Kind;
        Cpv = new CpvClassification(args.Cpv.Code.Trim(), args.Cpv.Name.Trim());
        Method = args.Method;
        BuyerId = args.BuyerId;
        ContractorId = args.ContractorId;
        ExpectedAmount = args.ExpectedAmount;
        ContractAmount = args.ContractAmount;
        Status = args.Status;
        SourceStatus = args.SourceStatus.Trim();
        Place = args.Place;
        PeriodStart = args.PeriodStart;
        PeriodEnd = args.PeriodEnd;
        NonCompetitiveBasis = args.NonCompetitiveBasis;
        BidsListComplete = args.BidsListComplete;
        SourceUrl = args.SourceUrl.Trim();
        VerifiedAt = args.VerifiedAt;
        VerifiedByUserId = args.VerifiedByUserId;
    }
 
    public static ErrorOr<Procurement> Create(ProcurementCreationParams args)
    {
        var validationResult = ValidateInvariants(args);
 
        if (validationResult.IsError)
            return validationResult.Errors;
 
        return new Procurement(Guid.CreateVersion7(), args);
    }
 
    public ErrorOr<ProcurementEvent> AddEvent(ProcurementEventCreationParams args)
    {
        var created = ProcurementEvent.Create(Id, _events.Count, args);
 
        if (created.IsError)
            return created.Errors;
 
        _events.Add(created.Value);
 
        return created;
    }
 
    public ErrorOr<Bid> AddBid(BidCreationParams args)
    {
        if (_bids.Any(b => b.BidderId == args.BidderId))
            return Error.Conflict("Procurement.DuplicateBidder", "This bidder already has a bid in this procurement.");
 
        if (args.Outcome == BidOutcome.Winner && _bids.Any(b => b.Outcome == BidOutcome.Winner))
            return Error.Conflict("Procurement.WinnerAlreadyExists", "A procurement can have only one winning bid.");
 
        var created = Bid.Create(Id, args);
 
        if (created.IsError)
            return created.Errors;
 
        _bids.Add(created.Value);
 
        return created;
    }
 
    public ErrorOr<FundingSource> AddFundingSource(FundingSourceCreationParams args)
    {
        var created = FundingSource.Create(Id, args);
 
        if (created.IsError)
            return created.Errors;
 
        _fundingSources.Add(created.Value);
 
        return created;
    }
 
    public ErrorOr<ProcurementNote> AddNote(string text, string sourceUrl)
    {
        var created = ProcurementNote.Create(Id, text, sourceUrl);
 
        if (created.IsError)
            return created.Errors;
 
        _notes.Add(created.Value);
 
        return created;
    }
 
    public ErrorOr<DataGap> AddDataGap(DataGapKind kind, string text)
    {
        var created = DataGap.Create(Id, kind, text);
 
        if (created.IsError)
            return created.Errors;
 
        _dataGaps.Add(created.Value);
 
        return created;
    }
 
    public ErrorOr<Updated> MarkVerified(DateOnly verifiedAt, Guid? verifiedByUserId)
    {
        VerifiedAt = verifiedAt;
        VerifiedByUserId = verifiedByUserId;
 
        return Result.Updated;
    }
 
    private static ErrorOr<Success> ValidateInvariants(ProcurementCreationParams args)
    {
        var errors = new List<Error>();
 
        if (string.IsNullOrWhiteSpace(args.ExternalId))
            errors.Add(Error.Validation("Procurement.ExternalIdRequired", "Procurement external id is required."));
 
        if (string.IsNullOrWhiteSpace(args.Title))
            errors.Add(Error.Validation("Procurement.TitleRequired", "Procurement title is required."));
 
        if (args.Cpv is null || string.IsNullOrWhiteSpace(args.Cpv.Code) || string.IsNullOrWhiteSpace(args.Cpv.Name))
            errors.Add(Error.Validation("Procurement.CpvRequired", "CPV code and name are required."));
 
        if (string.IsNullOrWhiteSpace(args.SourceStatus))
            errors.Add(Error.Validation("Procurement.SourceStatusRequired", "Source status is required."));
 
        if (string.IsNullOrWhiteSpace(args.SourceUrl))
            errors.Add(Error.Validation("Procurement.SourceUrlRequired", "Source URL is required."));
 
        if (args.BuyerId == Guid.Empty)
            errors.Add(Error.Validation("Procurement.BuyerRequired", "Buyer is required."));
 
        if (args.ExpectedAmount is { Value: < 0 })
            errors.Add(Error.Validation("Procurement.InvalidExpectedAmount", "Expected amount cannot be negative."));
 
        if (args.ContractAmount is { Value: < 0 })
            errors.Add(Error.Validation("Procurement.InvalidContractAmount", "Contract amount cannot be negative."));
 
        switch (args.Status)
        {
            case ProcurementStatus.ContractSigned:
                if (args.ContractorId is null || args.ContractAmount is null)
                    errors.Add(Error.Validation(
                        "Procurement.ContractDataRequired",
                        "A procurement with a signed contract requires a contractor and a contract amount."));
                break;
 
            case ProcurementStatus.NotHeld:
                if (args.ContractorId is not null || args.ContractAmount is not null)
                    errors.Add(Error.Validation(
                        "Procurement.UnexpectedContractData",
                        "A procurement that was not held cannot have a contractor or a contract amount."));
                break;
        }
 
        if (args.Place is null)
        {
            errors.Add(Error.Validation("Procurement.PlaceRequired", "Place is required (use PlaceType.Unknown when it is not known)."));
        }
        else
        {
            var place = args.Place;
 
            if ((place.Latitude is null) != (place.Longitude is null))
                errors.Add(Error.Validation("Procurement.IncompleteCoordinates", "Latitude and longitude must be provided together."));
 
            if ((place.Latitude is null) != (place.CoordinatesStatus is null))
                errors.Add(Error.Validation("Procurement.CoordinatesStatusMismatch", "Coordinates and their verification status must be provided together."));
 
            if (place.Latitude is < -90 or > 90)
                errors.Add(Error.Validation("Procurement.InvalidLatitude", "Latitude must be between -90 and 90."));
 
            if (place.Longitude is < -180 or > 180)
                errors.Add(Error.Validation("Procurement.InvalidLongitude", "Longitude must be between -180 and 180."));
        }
 
        if (args.PeriodStart is not null && args.PeriodEnd is not null && args.PeriodEnd < args.PeriodStart)
            errors.Add(Error.Validation("Procurement.InvalidPeriod", "Period end cannot be earlier than period start."));
 
        if (args.NonCompetitiveBasis is not null && args.Method != ProcurementMethod.NoElectronicSystem)
            errors.Add(Error.Validation(
                "Procurement.UnexpectedNonCompetitiveBasis",
                "A non-competitive basis is only possible for procurements without an electronic system."));
 
        return errors.Count > 0 ? errors : Result.Success;
    }
}