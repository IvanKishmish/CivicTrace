using CivicTrace.Application.Features.Auth.Common;
using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Auth.Register;

public sealed record RegisterCommand(string DisplayName, string Email, string Password)
    : ICommand<ErrorOr<TokenPair>>;