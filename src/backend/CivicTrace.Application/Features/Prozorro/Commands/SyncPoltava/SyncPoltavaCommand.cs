using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Prozorro.Commands.SyncPoltava;

public sealed record SyncPoltavaCommand(int? MaxPages = null, int? TargetNewCount = null)
    : ICommand<ErrorOr<SyncPoltavaResponse>>;

public sealed record SyncPoltavaResponse(
    int PagesScanned,
    int MatchedPoltava,
    int Imported,
    int SkippedExisting,
    int Failed,
    int GeocodedExisting);