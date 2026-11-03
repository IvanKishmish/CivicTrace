namespace CivicTrace.WebApi.Contracts.Procurements;

/// <summary>Якщо VerifiedAt не вказано, береться сьогоднішня дата (UTC).</summary>
public sealed record VerifyProcurementRequest(DateOnly? VerifiedAt);