using CivicTrace.Domain.Common;
using CivicTrace.Domain.Entities.Models;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

public sealed class FundingSource : Entity
{
    public Guid ProcurementId { get; private set; }

    public string Source { get; private set; } = string.Empty;
    public string? ExpenditureCode { get; private set; }
    public string? Territory { get; private set; }
    public decimal Amount { get; private set; }

    private FundingSource()
    { } // ef

    private FundingSource(Guid id, Guid procurementId, FundingSourceCreationParams args) : base(id)
    {
        ProcurementId = procurementId;
        Source = args.Source.Trim();
        ExpenditureCode = string.IsNullOrWhiteSpace(args.ExpenditureCode) ? null : args.ExpenditureCode.Trim();
        Territory = string.IsNullOrWhiteSpace(args.Territory) ? null : args.Territory.Trim();
        Amount = args.Amount;
    }

    internal static ErrorOr<FundingSource> Create(Guid procurementId, FundingSourceCreationParams args)
    {
        var validationResult = ValidateInvariants(args);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new FundingSource(Guid.CreateVersion7(), procurementId, args);
    }

    private static ErrorOr<Success> ValidateInvariants(FundingSourceCreationParams args)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(args.Source))
            errors.Add(Error.Validation("FundingSource.SourceRequired", "Funding source name is required."));

        if (args.Amount < 0)
            errors.Add(Error.Validation("FundingSource.InvalidAmount", "Funding amount cannot be negative."));

        return errors.Count > 0 ? errors : Result.Success;
    }
}