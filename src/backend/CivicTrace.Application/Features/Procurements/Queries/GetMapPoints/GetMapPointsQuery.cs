using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Procurements.Queries.GetMapPoints;

public sealed record GetMapPointsQuery : IQuery<ErrorOr<IReadOnlyList<MapPointResponse>>>;