using Microsoft.AspNetCore.Identity;

namespace CivicTrace.Persistence.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}