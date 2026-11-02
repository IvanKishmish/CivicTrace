namespace CivicTrace.Application.Common.Interfaces;

public sealed record RefreshTokenLookup(Guid UserId, Guid FamilyId, RefreshTokenStatus Status);