using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Common.Options;
using CivicTrace.Application.Features.Procurements.Common;
using CivicTrace.Application.Features.Prozorro.Common;
using CivicTrace.Domain.Entities;
using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CivicTrace.Application.Features.Prozorro.Commands.ImportProcurement;

public sealed class ImportProcurementCommandHandler(
    IProzorroClient prozorroClient,
    IAppDbContext dbContext,
    IValidator<ImportProcurementCommand> validator,
    IOptions<ProzorroOptions> prozorroOptions)
    : ICommandHandler<ImportProcurementCommand, ErrorOr<ImportProcurementResponse>>
{
    public async ValueTask<ErrorOr<ImportProcurementResponse>> Handle(
        ImportProcurementCommand command,
        CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var tenderResult = await prozorroClient.GetTenderAsync(command.ApiId.ToLowerInvariant(), ct);

        if (tenderResult.IsError)
            return tenderResult.Errors;

        var planResult = ProzorroTenderMapper.Map(
            tenderResult.Value,
            DateOnly.FromDateTime(DateTime.UtcNow),
            prozorroOptions.Value.BuildMethodTypeMap());

        if (planResult.IsError)
            return planResult.Errors;

        var plan = planResult.Value;

        var alreadyExists = await dbContext.Procurements.AnyAsync(p => p.ExternalId == plan.ExternalId, ct);

        if (alreadyExists && !command.DryRun && !command.ReplaceExisting)
            return Error.Conflict(
                "Prozorro.AlreadyImported",
                $"Procurement '{plan.ExternalId}' is already in the database. Set replaceExisting to true to import it again.");

        // ---- організації: знаходимо за ЄДРПОУ або створюємо
        var wanted = new Dictionary<string, ImportedOrganization>(StringComparer.Ordinal);

        void Want(ImportedOrganization? organization)
        {
            if (organization is not null)
                wanted.TryAdd(organization.Edrpou, organization);
        }

        Want(plan.Buyer);
        Want(plan.Contractor);
        foreach (var bidder in plan.Bidders)
            Want(bidder);

        var edrpous = wanted.Keys.ToList();

        var existingOrganizations = await dbContext.Organizations
            .Where(o => edrpous.Contains(o.Edrpou))
            .ToDictionaryAsync(o => o.Edrpou, StringComparer.Ordinal, ct);

        var newOrganizations = new List<Organization>();

        foreach (var (edrpou, imported) in wanted)
        {
            if (existingOrganizations.TryGetValue(edrpou, out var existing))
            {
                if (!command.DryRun && !string.IsNullOrWhiteSpace(imported.Address) && string.IsNullOrWhiteSpace(existing.Address))
                {
                    var updated = existing.UpdateDetails(existing.Name, imported.Address);

                    if (updated.IsError)
                        return updated.Errors;
                }

                continue;
            }

            var created = Organization.Create(new OrganizationCreationParams(imported.Name, imported.Edrpou, imported.Address));

            if (created.IsError)
                return created.Errors;

            newOrganizations.Add(created.Value);
        }

        Organization OrganizationOf(string edrpou)
            => existingOrganizations.TryGetValue(edrpou, out var found)
                ? found
                : newOrganizations.First(o => o.Edrpou == edrpou);

        var organizationsById = existingOrganizations.Values
            .Concat(newOrganizations)
            .ToDictionary(o => o.Id);

        // ---- закупівля
        var buyer = OrganizationOf(plan.Buyer.Edrpou);
        var contractor = plan.Contractor is null ? null : OrganizationOf(plan.Contractor.Edrpou);

        var procurementResult = Procurement.Create(new ProcurementCreationParams(
            ExternalId: plan.ExternalId,
            ApiId: plan.ApiId,
            PlanExternalId: null,
            Title: plan.Title,
            Kind: plan.Kind,
            Cpv: plan.Cpv,
            Method: plan.Method,
            BuyerId: buyer.Id,
            ContractorId: contractor?.Id,
            ExpectedAmount: plan.ExpectedAmount,
            ContractAmount: plan.ContractAmount,
            Status: plan.Status,
            SourceStatus: plan.SourceStatus,
            Place: plan.Place,
            PeriodStart: plan.PeriodStart,
            PeriodEnd: plan.PeriodEnd,
            NonCompetitiveBasis: plan.NonCompetitiveBasis,
            BidsListComplete: plan.BidsListComplete,
            SourceUrl: plan.SourceUrl,
            VerifiedAt: null,
            VerifiedByUserId: null));

        if (procurementResult.IsError)
            return procurementResult.Errors;

        var procurement = procurementResult.Value;

        foreach (var importedEvent in plan.Events)
        {
            var added = procurement.AddEvent(new ProcurementEventCreationParams(
                importedEvent.Date,
                importedEvent.Stage,
                importedEvent.Title,
                importedEvent.DateNote,
                importedEvent.SourceUrl));

            if (added.IsError)
                return added.Errors;
        }

        foreach (var importedBid in plan.Bids)
        {
            var added = procurement.AddBid(new BidCreationParams(
                OrganizationOf(importedBid.BidderEdrpou).Id,
                new MoneyAmount(
                    importedBid.Amount,
                    importedBid.VatIncluded,
                    plan.ExpectedAmount?.Currency ?? "UAH"),
                importedBid.Outcome));

            if (added.IsError)
                return added.Errors;
        }

        foreach (var funding in plan.Funding)
        {
            var added = procurement.AddFundingSource(funding);

            if (added.IsError)
                return added.Errors;
        }

        foreach (var note in plan.Notes)
        {
            var added = procurement.AddNote(note, plan.SourceUrl);

            if (added.IsError)
                return added.Errors;
        }

        foreach (var text in plan.MissingData)
        {
            var added = procurement.AddDataGap(DataGapKind.Missing, text);

            if (added.IsError)
                return added.Errors;
        }

        foreach (var text in plan.NeedsCheck)
        {
            var added = procurement.AddDataGap(DataGapKind.NeedsCheck, text);

            if (added.IsError)
                return added.Errors;
        }

        var response = ProcurementMapper.ToResponse(
            procurement,
            verifiedBy: null,
            buyer,
            contractor,
            bid => organizationsById[bid.BidderId]);

        var warnings = plan.Warnings.ToList();

        if (alreadyExists && command.DryRun)
            warnings.Add("Ця закупівля вже є в базі; під час реального імпорту потрібен replaceExisting = true.");

        if (command.DryRun)
            return new ImportProcurementResponse(false, alreadyExists, warnings, response);

        if (alreadyExists)
        {
            await dbContext.Procurements
                .Where(p => p.ExternalId == plan.ExternalId)
                .ExecuteDeleteAsync(ct);
        }

        dbContext.Organizations.AddRange(newOrganizations);
        dbContext.Procurements.Add(procurement);

        await dbContext.SaveChangesAsync(ct);

        return new ImportProcurementResponse(true, alreadyExists, warnings, response);
    }
}