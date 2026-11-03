using CivicTrace.Application.Common;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Common.Options;
using CivicTrace.Application.Features.Auth.Common;
using CivicTrace.Domain.Entities;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Options;

namespace CivicTrace.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IAppDbContext dbContext,
    IValidator<RegisterCommand> validator,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions)
    : ICommandHandler<RegisterCommand, ErrorOr<TokenPair>>
{
    public async ValueTask<ErrorOr<TokenPair>> Handle(RegisterCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var userIdResult = await identityService.CreateUserAsync(command.Email, command.Password, ct);

        if (userIdResult.IsError)
            return userIdResult.Errors;

        var profileResult = UserProfile.Create(userIdResult.Value, command.DisplayName);

        if (profileResult.IsError)
            return profileResult.Errors;

        dbContext.UserProfiles.Add(profileResult.Value);

        await dbContext.SaveChangesAsync(ct);

        var roles = await identityService.GetUserRolesAsync(userIdResult.Value, ct);

        var accessToken = tokenService.GenerateAccessToken(userIdResult.Value, command.Email, roles);
        var refreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = TokenHasher.Hash(refreshToken);

        var familyId = Guid.CreateVersion7();
        var refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenExpiryDays);

        await identityService
            .StoreRefreshTokenAsync(userIdResult.Value, familyId, refreshTokenHash, refreshLifetime, ct);

        return new TokenPair(accessToken, refreshToken);
    }
}