using CivicTrace.Domain.Common;
using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

public sealed class Bid : Entity
{
    public Guid ProcurementId { get; private set; }
    public Guid BidderId { get; private set; }
    public MoneyAmount Amount { get; private set; } = null!;

    /// <summary>null — на сторінці статус пропозиції не показано.</summary>
    public BidOutcome? Outcome { get; private set; }

    //nav props
    public Organization Bidder { get; private set; } = null!;

    private Bid()
    { } // ef

    private Bid(Guid id, Guid procurementId, BidCreationParams args) : base(id)
    {
        ProcurementId = procurementId;
        BidderId = args.BidderId;
        Amount = args.Amount;
        Outcome = args.Outcome;
    }

    internal static ErrorOr<Bid> Create(Guid procurementId, BidCreationParams args)
    {
        var validationResult = ValidateInvariants(args);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new Bid(Guid.CreateVersion7(), procurementId, args);
    }

    private static ErrorOr<Success> ValidateInvariants(BidCreationParams args)
    {
        var errors = new List<Error>();

        if (args.BidderId == Guid.Empty)
            errors.Add(Error.Validation("Bid.BidderRequired", "Bidder is required."));

        if (args.Amount is null || args.Amount.Value <= 0)
            errors.Add(Error.Validation("Bid.InvalidAmount", "Bid amount must be greater than zero."));

        return errors.Count > 0 ? errors : Result.Success;
    }
}