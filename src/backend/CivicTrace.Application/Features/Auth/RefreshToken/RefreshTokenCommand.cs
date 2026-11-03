using CivicTrace.Application.Features.Auth.Common;
using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Auth.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<ErrorOr<TokenPair>>;