namespace CivicTrace.Application.Features.Procurements.Queries.GetMapPoints;

public sealed record MapPointResponse(
    string Id,
    string Title,
    string? Address,
    string PlaceType,
    double Latitude,
    double Longitude,
    string? CoordinatesStatus,
    string Status,
    string StatusLabel,
    string Buyer,
    decimal? ExpectedAmount,
    decimal? ContractAmount,
    string Currency,
    bool? ExpectedVatIncluded,
    bool? ContractVatIncluded,
    string CpvCode);
