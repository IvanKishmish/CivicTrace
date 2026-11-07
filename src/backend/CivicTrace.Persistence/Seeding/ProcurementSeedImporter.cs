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

public sealed record ProcurementSeedImportResult(int Imported, int Skipped, int Replaced);

/// <summary>
/// Імпорт data/procurements.seed.json (опис полів — docs/seed-schema.md) в PostgreSQL.
/// За замовчуванням ідемпотентний: закупівлі, що вже є в базі (за ExternalId), пропускаються.
/// Seed:ReplaceExisting=true — видалити й завантажити заново закупівлі з файлу (корисно, поки файл правиться).
/// </summary>
public static class ProcurementSeedImporter
{
    private const string DefaultPath = "../../../data/procurements.seed.json";

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
        var configuredPath = configuration["Seed:ProcurementsPath"];
        var path = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configuredPath) ? DefaultPath : configuredPath,
            contentRootPath);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Procurement seed file was not found at '{path}'.", path);

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var records = JsonSerializer.Deserialize<List<SeedRecord>>(json, JsonOptions)
                      ?? throw new InvalidDataException("Procurement seed JSON must contain an array of records.");

        ValidateSeed(records);

        var replaceExisting = configuration.GetValue<bool>("Seed:ReplaceExisting");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var replaced = 0;

        if (replaceExisting)
        {
            var ids = records.Select(r => r.Id).ToList();

            // Дочірні сутності видаляються каскадом на рівні БД
            replaced = await db.Procurements
                .Where(p => ids.Contains(p.ExternalId))
                .ExecuteDeleteAsync(cancellationToken);
        }

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
                var procurement = BuildProcurement(record, organizations, db);

                db.Procurements.Add(procurement);
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

        return new ProcurementSeedImportResult(imported, skipped, replaced);
    }

    private static Procurement BuildProcurement(
        SeedRecord record,
        IDictionary<string, Organization> organizations,
        AppDbContext db)
    {
        var buyerId = ResolveOrganization(record.Buyer, record.BuyerEdrpou, record.BuyerAddress, organizations, db);

        Guid? contractorId = record.Contractor is null
            ? null
            : ResolveOrganization(record.Contractor, record.ContractorEdrpou!, null, organizations, db);

        var created = Procurement.Create(new ProcurementCreationParams(
            ExternalId: record.Id,
            ApiId: record.ApiId,
            PlanExternalId: record.PlanId,
            Title: record.Title,
            Kind: ParseKind(record.Kind),
            Cpv: new CpvClassification(record.CpvCode, record.CpvName),
            Method: ParseMethod(record.ProcurementType),
            BuyerId: buyerId,
            ContractorId: contractorId,
            ExpectedAmount: record.ExpectedAmount is { } expected
                ? new MoneyAmount(expected, record.ExpectedVatIncluded, record.Currency)
                : null,
            ContractAmount: record.ContractAmount is { } contract
                ? new MoneyAmount(contract, record.ContractVatIncluded, record.Currency)
                : null,
            Status: ParseStatus(record.Status),
            SourceStatus: record.SourceStatus,
            Place: new ProcurementPlace(
                record.Address,
                ParsePlaceType(record.PlaceType),
                record.Latitude,
                record.Longitude,
                ParseCoordinatesStatus(record.CoordinatesStatus)),
            PeriodStart: ParseDate(record.PeriodStart, "periodStart"),
            PeriodEnd: ParseDate(record.PeriodEnd, "periodEnd"),
            NonCompetitiveBasis: record.NonCompetitiveBasis is { } basis
                ? new NonCompetitiveBasis(basis.LegalBasis, basis.Justification, basis.SourceUrl)
                : null,
            BidsListComplete: record.BidsListComplete,
            SourceUrl: record.SourceUrl,
            VerifiedAt: ParseDate(record.VerifiedAt, "verifiedAt"),
            VerifiedByUserId: null));

        var procurement = Unwrap(created);

        foreach (var seedEvent in record.Events)
        {
            Unwrap(procurement.AddEvent(new ProcurementEventCreationParams(
                ParseDate(seedEvent.Date, "events.date"),
                ParseEventStage(seedEvent.Stage),
                seedEvent.Title,
                seedEvent.DateNote,
                seedEvent.SourceUrl)));
        }

        foreach (var seedBid in record.Bids)
        {
            var bidderId = ResolveOrganization(seedBid.Bidder, seedBid.BidderEdrpou, null, organizations, db);

            Unwrap(procurement.AddBid(new BidCreationParams(
                bidderId,
                new MoneyAmount(seedBid.Amount, seedBid.VatIncluded, record.Currency),
                ParseOutcome(seedBid.Outcome))));
        }

        foreach (var seedFunding in record.Funding)
        {
            Unwrap(procurement.AddFundingSource(new FundingSourceCreationParams(
                seedFunding.Source,
                seedFunding.ExpenditureCode,
                seedFunding.Territory,
                seedFunding.Amount)));
        }

        foreach (var seedNote in record.Notes)
            Unwrap(procurement.AddNote(seedNote.Text, seedNote.SourceUrl));

        foreach (var text in record.MissingData)
            Unwrap(procurement.AddDataGap(DataGapKind.Missing, text));

        foreach (var text in record.NeedsCheck)
            Unwrap(procurement.AddDataGap(DataGapKind.NeedsCheck, text));

        return procurement;
    }

    private static Guid ResolveOrganization(
        string name,
        string edrpou,
        string? address,
        IDictionary<string, Organization> organizations,
        AppDbContext db)
    {
        if (organizations.TryGetValue(edrpou, out var existing))
        {
            // Адресу дописуємо лише якщо її ще не було
            if (!string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(existing.Address))
                Unwrap(existing.UpdateDetails(existing.Name, address));

            return existing.Id;
        }

        var created = Unwrap(Organization.Create(new OrganizationCreationParams(name, edrpou, address)));

        db.Organizations.Add(created);
        organizations.Add(edrpou, created);

        return created.Id;
    }

    private static void ValidateSeed(IReadOnlyList<SeedRecord> records)
    {
        if (records.Count == 0)
            throw new InvalidDataException("Procurement seed JSON contains no records.");

        var duplicate = records
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .FirstOrDefault(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1);

        if (duplicate is not null)
            throw new InvalidDataException($"Seed file contains an empty or duplicate procurement id '{duplicate.Key}'.");

        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Buyer) ||
                string.IsNullOrWhiteSpace(r.BuyerEdrpou) || string.IsNullOrWhiteSpace(r.CpvCode) ||
                string.IsNullOrWhiteSpace(r.CpvName) || string.IsNullOrWhiteSpace(r.SourceUrl) ||
                string.IsNullOrWhiteSpace(r.SourceStatus))
                throw Invalid(r, "a required field is empty");

            if ((r.Contractor is null) != (r.ContractAmount is null))
                throw Invalid(r, "contractor and contractAmount must be both set or both null");

            if (r.Contractor is not null && string.IsNullOrWhiteSpace(r.ContractorEdrpou))
                throw Invalid(r, "contractorEdrpou is required when contractor is set");

            if (r.HasCoordinates != (r.Latitude is not null && r.Longitude is not null))
                throw Invalid(r, "hasCoordinates must match latitude/longitude presence");

            if (r.BidsListComplete && r.Bids.Count < 2)
                throw Invalid(r, "bidsListComplete requires at least two bids");

            var winner = r.Bids.FirstOrDefault(b => b.Outcome == "winner");
            if (winner is not null && r.ContractAmount is { } contractAmount && winner.Amount != contractAmount)
                throw Invalid(r, "the winning bid amount differs from contractAmount");

            if (r.Bids.Any(b => string.IsNullOrWhiteSpace(b.Bidder) || string.IsNullOrWhiteSpace(b.BidderEdrpou)))
                throw Invalid(r, "each bid requires bidder and bidderEdrpou");
        }
    }

    // ---------- коди й підписи сид-файлу -> enum

    private static ProcurementKind ParseKind(string label) => label switch
    {
        "Товари" => ProcurementKind.Goods,
        "Послуги" => ProcurementKind.Services,
        "Роботи" => ProcurementKind.Works,
        _ => throw new InvalidDataException($"unknown kind '{label}'")
    };

    private static ProcurementMethod ParseMethod(string label) => label switch
    {
        "Закупівля без використання електронної системи" => ProcurementMethod.NoElectronicSystem,
        "Запит (ціни) пропозицій" => ProcurementMethod.RequestForQuotations,
        "Відкриті торги з особливостями" => ProcurementMethod.OpenTendersWithSpecifics,
        _ => throw new InvalidDataException($"unknown procurementType '{label}'")
    };

    private static ProcurementStatus ParseStatus(string code) => code switch
    {
        "contract_signed" => ProcurementStatus.ContractSigned,
        "not_held" => ProcurementStatus.NotHeld,
        _ => throw new InvalidDataException($"unknown status '{code}'")
    };

    private static PlaceType ParsePlaceType(string code) => code switch
    {
        "unknown" => PlaceType.Unknown,
        "work" => PlaceType.Work,
        "delivery" => PlaceType.Delivery,
        _ => throw new InvalidDataException($"unknown placeType '{code}'")
    };

    private static CoordinatesStatus? ParseCoordinatesStatus(string? code) => code switch
    {
        null => null,
        "needs_check" => CoordinatesStatus.NeedsCheck,
        "verified" => CoordinatesStatus.Verified,
        _ => throw new InvalidDataException($"unknown coordinatesStatus '{code}'")
    };

    private static BidOutcome? ParseOutcome(string? code) => code switch
    {
        null => null,
        "winner" => BidOutcome.Winner,
        "rejected" => BidOutcome.Rejected,
        _ => throw new InvalidDataException($"unknown bid outcome '{code}'")
    };

    private static EventStage ParseEventStage(string code) => code switch
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
        _ => throw new InvalidDataException($"unknown event stage '{code}'")
    };

    private static DateOnly? ParseDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        throw new InvalidDataException($"{field} must use yyyy-MM-dd format; received '{value}'");
    }

    private static T Unwrap<T>(ErrorOr<T> result)
    {
        if (result.IsError)
            throw new InvalidDataException(string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));

        return result.Value;
    }

    private static InvalidDataException Invalid(SeedRecord record, string message)
        => new($"Procurement '{record.Id}': {message}.");

    // ---------- форма сид-файлу (docs/seed-schema.md); невідомі поля ігноруються

    private sealed class SeedRecord
    {
        public string Id { get; init; } = string.Empty;
        public string? ApiId { get; init; }
        public string? PlanId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string CpvCode { get; init; } = string.Empty;
        public string CpvName { get; init; } = string.Empty;
        public string ProcurementType { get; init; } = string.Empty;
        public string Buyer { get; init; } = string.Empty;
        public string BuyerEdrpou { get; init; } = string.Empty;
        public string? BuyerAddress { get; init; }
        public string? Contractor { get; init; }
        public string? ContractorEdrpou { get; init; }
        public decimal? ExpectedAmount { get; init; }
        public decimal? ContractAmount { get; init; }
        public string Currency { get; init; } = "UAH";
        public bool? ExpectedVatIncluded { get; init; }
        public bool? ContractVatIncluded { get; init; }
        public string Status { get; init; } = string.Empty;
        public string SourceStatus { get; init; } = string.Empty;
        public string? Address { get; init; }
        public string PlaceType { get; init; } = "unknown";
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public bool HasCoordinates { get; init; }
        public string? CoordinatesStatus { get; init; }
        public string? PeriodStart { get; init; }
        public string? PeriodEnd { get; init; }
        public List<SeedFunding> Funding { get; init; } = [];
        public SeedBasis? NonCompetitiveBasis { get; init; }
        public bool BidsListComplete { get; init; }
        public List<SeedBid> Bids { get; init; } = [];
        public List<SeedEvent> Events { get; init; } = [];
        public string SourceUrl { get; init; } = string.Empty;
        public List<SeedNote> Notes { get; init; } = [];
        public List<string> MissingData { get; init; } = [];
        public List<string> NeedsCheck { get; init; } = [];
        public string? VerifiedAt { get; init; }
    }

    private sealed class SeedFunding
    {
        public string Source { get; init; } = string.Empty;
        public string? ExpenditureCode { get; init; }
        public string? Territory { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class SeedBasis
    {
        public string LegalBasis { get; init; } = string.Empty;
        public string Justification { get; init; } = string.Empty;
        public string SourceUrl { get; init; } = string.Empty;
    }

    private sealed class SeedBid
    {
        public string Bidder { get; init; } = string.Empty;
        public string BidderEdrpou { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public bool? VatIncluded { get; init; }
        public string? Outcome { get; init; }
    }

    private sealed class SeedEvent
    {
        public string? Date { get; init; }
        public string Stage { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? DateNote { get; init; }
        public string SourceUrl { get; init; } = string.Empty;
    }

    private sealed class SeedNote
    {
        public string Text { get; init; } = string.Empty;
        public string SourceUrl { get; init; } = string.Empty;
    }
}