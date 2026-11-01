namespace CivicTrace.Domain.ValueObjects;

/// <summary>Обґрунтування неконкурентної закупівлі, як воно наведене на сторінці закупівлі.</summary>
public sealed record NonCompetitiveBasis(string LegalBasis, string Justification, string SourceUrl);