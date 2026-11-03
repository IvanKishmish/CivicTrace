namespace CivicTrace.Application.Features.Procurements.Queries.GetMapPoints;

public sealed record MapPointResponse(
    string Id,
    string Title,
    string? Address,
    string PlaceType,
    double Latitude,
    double Longitude,
    string? CoordinatesStatus);