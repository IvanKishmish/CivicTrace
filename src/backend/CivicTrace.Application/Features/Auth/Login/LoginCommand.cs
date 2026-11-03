using CivicTrace.Application.Features.Auth.Common;
using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<ErrorOr<TokenPair>>;