using System.Globalization;
using System.Text.Json;
using CivicTrace.Domain.Entities;
using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;
using CivicTrace.Persistence.Context;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence.Seeding;

public sealed record ProcurementSeedImportResult(int Imported, int Skipped);

/// <summary>
/// One-time/demo import for the reviewed JSON sample. Existing procurements are left untouched,
/// so re-running the import cannot duplicate records or overwrite manual edits.
/// </summary>
public static class ProcurementSeedImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ProcurementSeedImportResult> SeedProcurementsAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        string contentRootPath,
        CancellationToken cancellationToken = default)
    {
        var configuredPath = configuration["Seed:ProcurementsPath"]
                             ?? "../../../data/mock-procurement.json";
        var path = Path.GetFullPath(configuredPath, contentRootPath);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Procurement seed file was not found at '{path}'.", path);

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var records = JsonSerializer.Deserialize<List<ProcurementSeedRecord>>(json, JsonOptions)
                      ?? throw new InvalidDataException("Procurement seed JSON must contain an array of records.");

        ValidateSeed(records);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existingIds = await db.Procurements
            .Select(p => p.ExternalId)
            .ToHashSetAsync(cancellationToken);
        var organizations = await db.Organizations
            .ToDictionaryAsync(o => o.Edrpou, StringComparer.Ordinal, cancellationToken);

        var imported = 0;
        var skipped = 0;

        foreach (var record in records)
        {
            if (existingIds.Contains(record.Id))
            {
                skipped++;
                continue;
            }

            try
            {
                var buyerId = ResolveOrganization(record.Buyer, record.BuyerEdrpou, organizations, db);
                Guid? contractorId = record.Contractor is null
                    ? null
                    : ResolveOrganization(
                        record.Contractor,
                        record.ContractorEdrpou ?? throw Invalid(record, "contractorEdrpou is required when contractor is set"),
                        organizations,
                        db);

                var status = ParseStatus(record.StatusCode, record);
                var procurement = Procurement.Create(new ProcurementCreationParams(
                    ExternalId: record.Id,
                    ApiId: null,
                    PlanExternalId: null,
                    Title: record.Title,
                    Kind: ParseKind(record.KindCode, record),
                    Cpv: new CpvClassification(record.CpvCode, record.CpvName),
                    Method: ParseMethod(record.MethodCode, record),
                    BuyerId: buyerId,
                    ContractorId: contractorId,
                    ExpectedAmount: record.ExpectedAmount is { } expected
                        ? new MoneyAmount(expected, record.ExpectedVatIncluded, record.Currency)
                        : null,
                    ContractAmount: record.ContractAmount is { } contract
                        ? new MoneyAmount(contract, record.ContractVatIncluded, record.Currency)
                        : null,
                    Status: status,
                    SourceStatus: record.Status,
                    Place: new ProcurementPlace(
                        record.Address,
                        ParsePlaceType(record.PlaceType, record),
                        record.Latitude,
                        record.Longitude,
                        ParseCoordinatesStatus(record.CoordinatesStatus, record)),
                    PeriodStart: ParseDate(record.StartDate, record, "startDate"),
                    PeriodEnd: ParseDate(record.EndDate, record, "endDate"),
                    NonCompetitiveBasis: null,
                    BidsListComplete: false,
                    SourceUrl: record.SourceUrl,
                    VerifiedAt: null,
                    VerifiedByUserId: null));

                if (procurement.IsError)
                    throw Invalid(record, FormatErrors(procurement.Errors));

                foreach (var seedEvent in record.Events)
                {
                    var addedEvent = procurement.Value.AddEvent(new ProcurementEventCreationParams(
                        ParseDate(seedEvent.Date, record, "events.date"),
                        ParseEventStage(seedEvent.Stage, record),
                        seedEvent.Title,
                        DateNote: null,
                        seedEvent.SourceUrl));

                    if (addedEvent.IsError)
                        throw Invalid(record, FormatErrors(addedEvent.Errors));
                }

                if (!string.IsNullOrWhiteSpace(record.MissingDataNotice))
                {
                    var addedNote = procurement.Value.AddNote(record.MissingDataNotice, record.SourceUrl);
                    if (addedNote.IsError)
                        throw Invalid(record, FormatErrors(addedNote.Errors));
                }

                if (record.Latitude is null)
                {
                    var dataGap = procurement.Value.AddDataGap(
                        DataGapKind.NeedsCheck,
                        "Точне місце виконання або поставки не підтверджене; точку на мапі не показуємо.");
                    if (dataGap.IsError)
                        throw Invalid(record, FormatErrors(dataGap.Errors));
                }

                db.Procurements.Add(procurement.Value);
                existingIds.Add(record.Id);
                imported++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidDataException($"Could not import procurement '{record.Id}': {ex.Message}", ex);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ProcurementSeedImportResult(imported, skipped);
    }

    private static Guid ResolveOrganization(
        string name,
        string edrpou,
        IDictionary<string, Organization> organizations,
        AppDbContext db)
    {
        if (organizations.TryGetValue(edrpou, out var existing))
            return existing.Id;

        var created = Organization.Create(new OrganizationCreationParams(name, edrpou, Address: null));
        if (created.IsError)
            throw new InvalidDataException(FormatErrors(created.Errors));

        db.Organizations.Add(created.Value);
        organizations.Add(edrpou, created.Value);
        return created.Value.Id;
    }

    private static void ValidateSeed(IReadOnlyList<ProcurementSeedRecord> records)
    {
        if (records.Count == 0)
            throw new InvalidDataException("Procurement seed JSON contains no records.");

        var duplicateId = records.GroupBy(x => x.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicateId is not null)
            throw new InvalidDataException($"Seed file contains an empty or duplicate procurement ID '{duplicateId.Key}'.");

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Title) || string.IsNullOrWhiteSpace(record.Buyer) ||
                string.IsNullOrWhiteSpace(record.BuyerEdrpou) || string.IsNullOrWhiteSpace(record.CpvCode) ||
                string.IsNullOrWhiteSpace(record.CpvName) || string.IsNullOrWhiteSpace(record.SourceUrl) ||
                string.IsNullOrWhiteSpace(record.Status) || string.IsNullOrWhiteSpace(record.Currency))
                throw Invalid(record, "a required field is empty");

            if (record.HasCoordinates != (record.Latitude is not null && record.Longitude is not null))
                throw Invalid(record, "hasCoordinates must match latitude/longitude presence");

            if ((record.Latitude is null) != (record.CoordinatesStatus is null))
                throw Invalid(record, "coordinatesStatus must be set only when coordinates are present");

        if (record.Contractor is null && record.ContractorEdrpou is not null)
                throw Invalid(record, "contractorEdrpou must be null when contractor is null");

            if (record.Events is null)
                throw Invalid(record, "events must be an array (use [] when there are no events)");

            if (record.Events.Any(seedEvent => string.IsNullOrWhiteSpace(seedEvent.Title) ||
                                                string.IsNullOrWhiteSpace(seedEvent.SourceUrl) ||
                                                string.IsNullOrWhiteSpace(seedEvent.Stage)))
                throw Invalid(record, "each event requires title, sourceUrl, and stage");
        }
    }

    private static ProcurementKind ParseKind(string code, ProcurementSeedRecord record) => code switch
    {
        "goods" => ProcurementKind.Goods,
        "services" => ProcurementKind.Services,
        "works" => ProcurementKind.Works,
        _ => throw Invalid(record, $"unknown kind code '{code}'")
    };

    private static ProcurementMethod ParseMethod(string code, ProcurementSeedRecord record) => code switch
    {
        "no_electronic_system" => ProcurementMethod.NoElectronicSystem,
        "request_for_quotations" => ProcurementMethod.RequestForQuotations,
        "open_tenders_with_specifics" => ProcurementMethod.OpenTendersWithSpecifics,
        _ => throw Invalid(record, $"unknown method code '{code}'")
    };

    private static ProcurementStatus ParseStatus(string code, ProcurementSeedRecord record) => code switch
    {
        "contract_signed" => ProcurementStatus.ContractSigned,
        "not_held" => ProcurementStatus.NotHeld,
        _ => throw Invalid(record, $"unknown status code '{code}'")
    };

    private static PlaceType ParsePlaceType(string code, ProcurementSeedRecord record) => code switch
    {
        "unknown" => PlaceType.Unknown,
        "work" => PlaceType.Work,
        "delivery" => PlaceType.Delivery,
        _ => throw Invalid(record, $"unknown placeType code '{code}'")
    };

    private static CoordinatesStatus? ParseCoordinatesStatus(string? code, ProcurementSeedRecord record) => code switch
    {
        null => null,
        "needs_check" => CoordinatesStatus.NeedsCheck,
        "verified" => CoordinatesStatus.Verified,
        _ => throw Invalid(record, $"unknown coordinatesStatus code '{code}'")
    };

    private static EventStage ParseEventStage(string code, ProcurementSeedRecord record) => code switch
    {
        "announced" => EventStage.Announced,
        "protocol_published" => EventStage.ProtocolPublished,
        "clarification_deadline" => EventStage.ClarificationDeadline,
        "bids_deadline" => EventStage.BidsDeadline,
        "bids_opened" => EventStage.BidsOpened,
        "bid_rejected" => EventStage.BidRejected,
        "award_notice" => EventStage.AwardNotice,
        "reported" => EventStage.Reported,
        "contract_signed" => EventStage.ContractSigned,
        "cancelled" => EventStage.Cancelled,
        _ => throw Invalid(record, $"unknown event stage '{code}'")
    };

    private static DateOnly? ParseDate(string? value, ProcurementSeedRecord record, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        throw Invalid(record, $"{field} must use yyyy-MM-dd format; received '{value}'");
    }

    private static InvalidDataException Invalid(ProcurementSeedRecord record, string message)
        => new($"Procurement '{record.Id}': {message}.");

    private static string FormatErrors(IEnumerable<Error> errors)
        => string.Join("; ", errors.Select(error => $"{error.Code}: {error.Description}"));

    private sealed class ProcurementSeedRecord
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string KindCode { get; init; } = string.Empty;
        public string CpvCode { get; init; } = string.Empty;
        public string CpvName { get; init; } = string.Empty;
        public string Buyer { get; init; } = string.Empty;
        public string BuyerEdrpou { get; init; } = string.Empty;
        public string? Contractor { get; init; }
        public string? ContractorEdrpou { get; init; }
        public decimal? ExpectedAmount { get; init; }
        public decimal? ContractAmount { get; init; }
        public string Currency { get; init; } = "UAH";
        public bool? ExpectedVatIncluded { get; init; }
        public bool? ContractVatIncluded { get; init; }
        public string Status { get; init; } = string.Empty;
        public string StatusCode { get; init; } = string.Empty;
        public string ProcurementType { get; init; } = string.Empty;
        public string MethodCode { get; init; } = string.Empty;
        public string? Address { get; init; }
        public string PlaceType { get; init; } = "unknown";
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public bool HasCoordinates { get; init; }
        public string? CoordinatesStatus { get; init; }
        public string? StartDate { get; init; }
        public string? EndDate { get; init; }
        public List<ProcurementSeedEvent> Events { get; init; } = [];
        public string SourceUrl { get; init; } = string.Empty;
        public string? MissingDataNotice { get; init; }
    }

    private sealed class ProcurementSeedEvent
    {
        public string? Date { get; init; }
        public string Stage { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string SourceUrl { get; init; } = string.Empty;
    }
}
