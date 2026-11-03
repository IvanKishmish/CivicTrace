using CivicTrace.Application.Common;
using CivicTrace.Application.Common.Interfaces;
using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler(IIdentityService identityService)
    : ICommandHandler<LogoutCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(LogoutCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
            return Result.Success;

        var incomingHash = TokenHasher.Hash(command.RefreshToken);
        await identityService.RevokeRefreshTokenAsync(incomingHash, ct);

        return Result.Success;
    }
}