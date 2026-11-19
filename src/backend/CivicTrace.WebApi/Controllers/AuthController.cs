using CivicTrace.Application.Common.Options;
using CivicTrace.Application.Features.Auth.Login;
using CivicTrace.Application.Features.Auth.Logout;
using CivicTrace.Application.Features.Auth.RefreshToken;
using CivicTrace.Application.Features.Auth.Register;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CivicTrace.WebApi.Controllers;

[EnableRateLimiting("auth")]
public sealed class AuthController(IMediator mediator, IOptions<JwtOptions> jwtOptions) : ApiController(mediator)
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);

        if (result.IsError)
            return HandleResult(result);

        SetRefreshTokenCookie(result.Value.RefreshToken);

        return Ok(new
        {
            AccessToken = result.Value.AccessToken
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);

        if (result.IsError)
            return HandleResult(result);

        SetRefreshTokenCookie(result.Value.RefreshToken);

        return Ok(new
        {
            AccessToken = result.Value.AccessToken
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct = default)
    {
        var refreshToken = Request.Cookies["refresh_token"];

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized();

        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken), ct);

        if (result.IsError)
        {
            Response.Cookies.Delete("refresh_token");
            return HandleResult(result);
        }

        SetRefreshTokenCookie(result.Value.RefreshToken);

        return Ok(new
        {
            AccessToken = result.Value.AccessToken
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct = default)
    {
        var refreshToken = Request.Cookies["refresh_token"];

        if (!string.IsNullOrEmpty(refreshToken))
            await Mediator.Send(new LogoutCommand(refreshToken), ct);

        Response.Cookies.Delete("refresh_token");

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me([FromServices] CivicTrace.Application.Common.Interfaces.ICurrentUserService currentUser)
    {
        if (currentUser.UserId is null)
            return Unauthorized();

        return Ok(new
        {
            UserId = currentUser.UserId,
            Email = currentUser.Email,
            Roles = currentUser.Roles
        });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var isLocalOrHttps = Request.IsHttps || Request.Host.Host is "localhost" or "127.0.0.1";

        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isLocalOrHttps,
            SameSite = isLocalOrHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpiryDays)
        });
    }
}
