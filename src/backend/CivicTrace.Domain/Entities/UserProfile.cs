using CivicTrace.Domain.Common;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

/// <summary>
/// Доменний профіль користувача. Сам обліковий запис (email, пароль, ролі) живе в ASP.NET Identity
/// в Infrastructure; Id профілю дорівнює Id користувача Identity (як Player у QuizArena).
/// </summary>
public sealed class UserProfile : Entity
{
    public string DisplayName { get; private set; } = string.Empty;

    private UserProfile()
    { } // ef

    private UserProfile(Guid id, string displayName) : base(id)
    {
        DisplayName = displayName.Trim();
    }

    public static ErrorOr<UserProfile> Create(Guid id, string displayName)
    {
        var validationResult = ValidateInvariants(id, displayName);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new UserProfile(id, displayName);
    }

    public ErrorOr<Updated> UpdateDisplayName(string newDisplayName)
    {
        var validationResult = ValidateInvariants(Id, newDisplayName);

        if (validationResult.IsError)
            return validationResult.Errors;

        DisplayName = newDisplayName.Trim();

        return Result.Updated;
    }

    private static ErrorOr<Success> ValidateInvariants(Guid id, string displayName)
    {
        var errors = new List<Error>();

        if (id == Guid.Empty)
            errors.Add(Error.Validation("UserProfile.IdRequired", "User id is required."));

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length < 2)
            errors.Add(Error.Validation("UserProfile.InvalidDisplayName", "Display name must be at least 2 characters long."));

        if (displayName?.Trim().Length > 100)
            errors.Add(Error.Validation("UserProfile.DisplayNameTooLong", "Display name cannot be longer than 100 characters."));

        return errors.Count > 0 ? errors : Result.Success;
    }
}