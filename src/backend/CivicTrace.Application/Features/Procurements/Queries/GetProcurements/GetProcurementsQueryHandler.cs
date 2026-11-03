using CivicTrace.Application.Common;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Procurements.Common;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Procurements.Queries.GetProcurements;

public sealed class GetProcurementsQueryHandler(
    IAppDbContext dbContext,
    IValidator<GetProcurementsQuery> validator)
    : IQueryHandler<GetProcurementsQuery, ErrorOr<PagedResponse<ProcurementResponse>>>
{
    public async ValueTask<ErrorOr<PagedResponse<ProcurementResponse>>> Handle(
        GetProcurementsQuery request,
        CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(request, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var query = dbContext.Procurements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";

            query = query.Where(p =>
                EF.Functions.ILike(p.Title, pattern) ||
                EF.Functions.ILike(p.ExternalId, pattern) ||
                EF.Functions.ILike(p.Buyer.Name, pattern));
        }

        if (ProcurementMapper.ParseStatus(request.Status) is { } status)
            query = query.Where(p => p.Status == status);

        if (ProcurementMapper.ParseKind(request.Kind) is { } kind)
            query = query.Where(p => p.Kind == kind);

        if (ProcurementMapper.ParseMethod(request.Method) is { } method)
            query = query.Where(p => p.Method == method);

        if (!string.IsNullOrEmpty(request.BuyerEdrpou))
            query = query.Where(p => p.Buyer.Edrpou == request.BuyerEdrpou);

        if (request.RepairOnly)
            query = query.Where(p => p.Cpv.Code.StartsWith("45"));

        if (request.Year is { } year)
        {
            // Рік = рік найранішої події з датою (як year у плані фронтенду)
            query = query.Where(p => p.Events
                .Where(e => e.Date != null)
                .OrderBy(e => e.Date)
                .Select(e => e.Date!.Value.Year)
                .FirstOrDefault() == year);
        }

        var totalCount = await query.CountAsync(ct);

        // ExternalId починається з дати (UA-РРРР-ММ-ДД-...), тож сортування за ним дає «спершу новіші»
        var items = await query
            .OrderByDescending(p => p.ExternalId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .IncludeFull()
            .ToListAsync(ct);

        var verifierNames = await dbContext.LoadVerifierNamesAsync(items, ct);

        var responses = items
            .Select(p => ProcurementMapper.ToResponse(p, verifierNames.VerifierName(p)))
            .ToList();

        return new PagedResponse<ProcurementResponse>(responses, request.PageNumber, request.PageSize, totalCount);
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}