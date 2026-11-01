namespace CivicTrace.Domain.Entities.Models;

public sealed record OrganizationCreationParams(
    string Name,
    string Edrpou,
    string? Address);