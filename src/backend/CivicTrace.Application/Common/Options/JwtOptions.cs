using System.ComponentModel.DataAnnotations;

namespace CivicTrace.Application.Common.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpiryMinutes { get; init; }

    [Range(1, 365)]
    public int RefreshTokenExpiryDays { get; init; } = 7;
}