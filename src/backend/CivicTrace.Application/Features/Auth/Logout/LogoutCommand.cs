using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string? RefreshToken) : ICommand<ErrorOr<Success>>;