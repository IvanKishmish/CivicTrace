namespace CivicTrace.Domain.Entities.Models;

public sealed record FundingSourceCreationParams(
    string Source,
    string? ExpenditureCode,
    string? Territory,
    decimal Amount);