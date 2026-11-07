using CivicTrace.Application.Features.Procurements.Common;

namespace CivicTrace.Application.Features.Prozorro.Commands.ImportProcurement;

public sealed record ImportProcurementResponse(
    bool Saved,
    bool AlreadyExisted,
    IReadOnlyList<string> Warnings,
    ProcurementResponse Procurement);