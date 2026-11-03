using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CivicTrace.Application.Common.Interfaces;

namespace CivicTrace.WebApi.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(sub, out var guid) ? guid : null;
        }
    }

    public string? Email => httpContextAccessor.HttpContext?.User
        .FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public IReadOnlyList<string> Roles => httpContextAccessor.HttpContext?.User
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? [];
}