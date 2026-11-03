using ErrorOr;

namespace CivicTrace.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<ErrorOr<Guid>> CreateUserAsync(string email, string password, CancellationToken ct = default);
    Task<ErrorOr<Guid>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    Task<ErrorOr<Updated>> AssignRoleAsync(Guid userId, string role, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<bool> IsUserLockedOutAsync(Guid userId, CancellationToken ct = default);

    Task StoreRefreshTokenAsync(Guid userId, Guid familyId, string refreshTokenHash, TimeSpan lifetime, CancellationToken ct = default);
    Task<RefreshTokenLookup?> FindRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshTokenHash, CancellationToken ct = default);
    Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken ct = default);
    Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct = default);
}