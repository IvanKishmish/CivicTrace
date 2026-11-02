namespace CivicTrace.Application.Common.Interfaces;

/// <summary>
/// Поточний користувач. Реалізація живе у WebApi (HttpContext). Поза HTTP-запитом (імпорт, фонові задачі) UserId = null.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
}