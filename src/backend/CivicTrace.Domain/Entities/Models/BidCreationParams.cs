using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;

namespace CivicTrace.Domain.Entities.Models;

public sealed record BidCreationParams(
    Guid BidderId,
    MoneyAmount Amount,
    BidOutcome? Outcome);