using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Export.Queries.ExportProcurements;

/// <summary>Format: csv | json.</summary>
public sealed record ExportProcurementsQuery(string Format = "csv") : IQuery<ErrorOr<ExportFile>>;