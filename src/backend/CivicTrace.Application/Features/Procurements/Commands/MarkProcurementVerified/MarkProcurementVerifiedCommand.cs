using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Procurements.Commands.MarkProcurementVerified;

/// <summary>Позначає закупівлю перевіреною. Якщо дату не вказано, береться сьогоднішня (UTC).</summary>
public sealed record MarkProcurementVerifiedCommand(string ExternalId, DateOnly? VerifiedAt = null)
    : ICommand<ErrorOr<Updated>>;