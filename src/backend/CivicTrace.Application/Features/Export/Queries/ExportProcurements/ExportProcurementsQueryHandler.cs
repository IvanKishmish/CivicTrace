using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Procurements.Common;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Export.Queries.ExportProcurements;

public sealed class ExportProcurementsQueryHandler(
    IAppDbContext dbContext,
    IValidator<ExportProcurementsQuery> validator)
    : IQueryHandler<ExportProcurementsQuery, ErrorOr<ExportFile>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async ValueTask<ErrorOr<ExportFile>> Handle(ExportProcurementsQuery query, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(query, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var procurements = await dbContext.Procurements
            .AsNoTracking()
            .OrderByDescending(p => p.ExternalId)
            .IncludeFull()
            .ToListAsync(ct);

        var verifierNames = await dbContext.LoadVerifierNamesAsync(procurements, ct);

        var responses = procurements
            .Select(p => ProcurementMapper.ToResponse(p, verifierNames.VerifierName(p)))
            .ToList();

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");

        if (query.Format == "json")
        {
            return new ExportFile(
                JsonSerializer.SerializeToUtf8Bytes(responses, JsonOptions),
                "application/json; charset=utf-8",
                $"civictrace-procurements-{stamp}.json");
        }

        // UTF-8 із BOM, щоб Excel правильно читав кирилицю
        var csvBytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(ProcurementsCsv.Build(responses)))
            .ToArray();

        return new ExportFile(csvBytes, "text/csv; charset=utf-8", $"civictrace-procurements-{stamp}.csv");
    }
}