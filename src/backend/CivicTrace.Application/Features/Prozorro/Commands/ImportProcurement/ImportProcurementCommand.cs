using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Prozorro.Commands.ImportProcurement;

/// <summary>
/// Імпорт закупівлі з Prozorro API за внутрішнім ідентифікатором (32 hex-символи, apiId).
/// DryRun = true лише показує, що було б збережено, і нічого не пише в базу.
/// </summary>
public sealed record ImportProcurementCommand(string ApiId, bool DryRun = true, bool ReplaceExisting = false)
    : ICommand<ErrorOr<ImportProcurementResponse>>;