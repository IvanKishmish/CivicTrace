using System.Globalization;
using CivicTrace.Application.Features.Prozorro.Models;
using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using CivicTrace.Domain.ValueObjects;
using ErrorOr;

namespace CivicTrace.Application.Features.Prozorro.Common;

/// <summary>
/// Чисте перетворення відповіді Prozorro API на ProzorroImportPlan. Нічого не вгадує: те, чого немає у відповіді,
/// потрапляє в MissingData/NeedsCheck, а не вигадується. Непідтримувані випадки повертаються як помилки.
/// </summary>
public static class ProzorroTenderMapper
{
    private const string EdrScheme = "UA-EDR";

    public static ErrorOr<ProzorroImportPlan> Map(
        ProzorroTender tender,
        DateOnly importedOn,
        IReadOnlyDictionary<string, ProcurementMethod> methodTypeMap)
    {
        var errors = new List<Error>();
        var missing = new List<string>();
        var needsCheck = new List<string>();
        var warnings = new List<string>();

        var externalId = Clean(tender.TenderID);
        if (externalId is null)
            errors.Add(Error.Validation("Prozorro.TenderIdMissing", "The tender response has no tenderID."));

        var title = Clean(tender.Title);
        if (title is null)
            errors.Add(Error.Validation("Prozorro.TitleMissing", "The tender response has no title."));

        // ---- спосіб закупівлі
        var method = default(ProcurementMethod);
        var methodType = Clean(tender.ProcurementMethodType);
        if (methodType is null || !methodTypeMap.TryGetValue(methodType, out method))
        {
            errors.Add(Error.Validation(
                "Prozorro.UnsupportedMethod",
                $"Unsupported procurementMethodType '{methodType}'. Add it to Prozorro:MethodTypeMap in configuration " +
                $"(value: one of {string.Join(", ", Enum.GetNames<ProcurementMethod>())})."));
        }

        // ---- тип предмету
        ProcurementKind? kind = tender.MainProcurementCategory switch
        {
            "goods" => ProcurementKind.Goods,
            "services" => ProcurementKind.Services,
            "works" => ProcurementKind.Works,
            _ => null
        };

        if (kind is null)
            errors.Add(Error.Validation("Prozorro.KindMissing", "mainProcurementCategory is missing or unsupported."));

        // ---- код ДК 021
        var classification = (tender.Items ?? [])
            .Select(i => i.Classification)
            .FirstOrDefault(c => c is not null && !string.IsNullOrWhiteSpace(c.Id) && IsCpvScheme(c.Scheme));

        if (classification is null)
            errors.Add(Error.Validation("Prozorro.CpvMissing", "No ДК 021 classification was found in the tender items."));

        // ---- статус
        ProcurementStatus status = default;
        var sourceStatus = string.Empty;
        switch (tender.Status)
        {
            case "complete":
                status = ProcurementStatus.ContractSigned;
                sourceStatus = "Завершена";
                break;

            case "unsuccessful":
                status = ProcurementStatus.NotHeld;
                sourceStatus = "Торги не відбулися";
                break;

            default:
                errors.Add(Error.Validation(
                    "Prozorro.UnsupportedStatus",
                    $"Tender status '{tender.Status}' is not supported yet; only 'complete' and 'unsuccessful' can be imported."));
                break;
        }

        // ---- замовник
        var buyer = ToOrganization(tender.ProcuringEntity);
        if (buyer is null)
            errors.Add(Error.Validation(
                "Prozorro.BuyerMissing",
                "The procuring entity has no name or no UA-EDR identifier."));

        if (errors.Count > 0)
            return errors;

        var sourceUrl = $"https://prozorro.gov.ua/tender/{externalId}";
        var cpv = new CpvClassification(classification!.Id!.Trim(), Clean(classification.Description) ?? classification.Id!.Trim());

        // ---- договір, переможець, суми
        var awards = tender.Awards ?? [];
        var contracts = tender.Contracts ?? [];

        ImportedOrganization? contractor = null;
        MoneyAmount? contractAmount = null;
        ProzorroAward? winningAward = null;
        ProzorroContract? contract = null;

        if (status == ProcurementStatus.ContractSigned)
        {
            var activeAwards = awards.Where(a => a.Status == "active").ToList();

            if (activeAwards.Count != 1)
            {
                errors.Add(Error.Validation(
                    "Prozorro.AwardCount",
                    $"Expected exactly one active award, found {activeAwards.Count}. Multi-lot tenders are not supported yet."));
            }
            else
            {
                var award = activeAwards[0];
                winningAward = award;

                contract = contracts.FirstOrDefault(c => c.Status == "active" && (c.AwardID is null || c.AwardID == award.Id))
                           ?? contracts.FirstOrDefault(c => c.Status == "active");

                if (contract is null)
                    errors.Add(Error.Validation("Prozorro.ContractMissing", "No active (signed) contract was found."));

                contractor = ToOrganization(award.Suppliers?.FirstOrDefault());
                if (contractor is null)
                    errors.Add(Error.Validation(
                        "Prozorro.ContractorMissing",
                        "The winning supplier has no name or no UA-EDR identifier."));

                contractAmount = ToMoney(contract?.Value);
                if (contractAmount is null)
                {
                    contractAmount = ToMoney(award.Value);

                    if (contractAmount is null)
                        errors.Add(Error.Validation("Prozorro.ContractAmountMissing", "No contract or award amount was found."));
                    else
                        needsCheck.Add("Суму договору в API не знайдено; використано суму переможної пропозиції.");
                }
            }

            if (errors.Count > 0)
                return errors;
        }

        // ---- пропозиції
        var bidders = new Dictionary<string, ImportedOrganization>(StringComparer.Ordinal);
        var bids = new List<ImportedBid>();
        var bidsComplete = false;

        if (status == ProcurementStatus.ContractSigned)
        {
            var winnerEdrpou = contractor?.Edrpou;

            var rejectedEdrpous = awards
                .Where(a => a.Status == "unsuccessful")
                .Select(a => Clean(a.Suppliers?.FirstOrDefault()?.Identifier?.Id))
                .Where(id => id is not null)
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);

            if (method == ProcurementMethod.NoElectronicSystem)
            {
                var amount = ToMoney(winningAward?.Value) ?? contractAmount;

                if (contractor is not null && amount is not null)
                {
                    bidders[contractor.Edrpou] = contractor;
                    bids.Add(new ImportedBid(contractor.Edrpou, amount.Value, amount.VatIncluded, BidOutcome.Winner));
                }
            }
            else if (tender.Bids is { Count: > 0 })
            {
                foreach (var bid in tender.Bids)
                {
                    var organization = ToOrganization(bid.Tenderers?.FirstOrDefault());
                    var amount = ToMoney(bid.Value);

                    if (organization is null || amount is null)
                    {
                        warnings.Add("Пропозицію без придатних даних про учасника чи суму пропущено.");
                        continue;
                    }

                    if (bidders.ContainsKey(organization.Edrpou))
                    {
                        warnings.Add($"Другу пропозицію того самого учасника (ЄДРПОУ {organization.Edrpou}) пропущено.");
                        continue;
                    }

                    bidders[organization.Edrpou] = organization;

                    BidOutcome? outcome = organization.Edrpou == winnerEdrpou
                        ? BidOutcome.Winner
                        : rejectedEdrpous.Contains(organization.Edrpou) ? BidOutcome.Rejected : null;

                    bids.Add(new ImportedBid(organization.Edrpou, amount.Value, amount.VatIncluded, outcome));
                }

                bidsComplete = bids.Count >= 2;
            }
            else
            {
                // Пропозиції в API не передано: показуємо лише учасників із рішеннями за результатами розгляду
                foreach (var award in awards)
                {
                    var organization = ToOrganization(award.Suppliers?.FirstOrDefault());
                    var amount = ToMoney(award.Value);

                    if (organization is null || amount is null || bidders.ContainsKey(organization.Edrpou))
                        continue;

                    BidOutcome? outcome = award.Status switch
                    {
                        "active" => BidOutcome.Winner,
                        "unsuccessful" => BidOutcome.Rejected,
                        _ => null
                    };

                    bidders[organization.Edrpou] = organization;
                    bids.Add(new ImportedBid(organization.Edrpou, amount.Value, amount.VatIncluded, outcome));
                }

                missing.Add("Реєстр пропозицій у відповіді API відсутній; показано лише учасників із рішеннями за результатами розгляду.");
            }
        }

        // ---- місце й період
        var item = (tender.Items ?? []).FirstOrDefault(i => i.DeliveryAddress is not null) ?? (tender.Items ?? []).FirstOrDefault();
        var address = FormatAddress(item?.DeliveryAddress);

        var placeType = address is null
            ? PlaceType.Unknown
            : kind == ProcurementKind.Works || cpv.Code.StartsWith("45", StringComparison.Ordinal)
                ? PlaceType.Work
                : PlaceType.Delivery;

        if (address is null)
            missing.Add("Місце поставки або виконання в даних закупівлі не вказано.");

        double? latitude = item?.DeliveryLocation?.Latitude;
        double? longitude = item?.DeliveryLocation?.Longitude;
        CoordinatesStatus? coordinatesStatus = null;

        if (latitude is not null && longitude is not null)
        {
            coordinatesStatus = CoordinatesStatus.NeedsCheck;
            needsCheck.Add("Координати надано замовником у даних закупівлі; команда їх не звіряла.");
        }
        else
        {
            latitude = null;
            longitude = null;
        }

        var place = new ProcurementPlace(address, placeType, latitude, longitude, coordinatesStatus);

        // ---- підстава неконкурентної закупівлі
        NonCompetitiveBasis? basis = null;

        if (method == ProcurementMethod.NoElectronicSystem)
        {
            var causeTitle = Clean(tender.CauseDetails?.Title);

            if (causeTitle is not null)
                basis = new NonCompetitiveBasis(causeTitle, Clean(tender.CauseDetails?.Description) ?? causeTitle, sourceUrl);
            else
                missing.Add("Обґрунтування вибору неконкурентної процедури у відповіді API не передано.");
        }

        // ---- події
        var events = new List<ImportedEvent>();

        void AddEvent(DateOnly? date, EventStage stage, string text, string? dateNote = null)
            => events.Add(new ImportedEvent(date, stage, text, dateNote, sourceUrl));

        var announcedDate = ToDate(tender.TenderPeriod?.StartDate);
        if (announcedDate is not null)
            AddEvent(announcedDate, EventStage.Announced, "Оголошення про проведення закупівлі");
        else if (ToDate(tender.Date) is { } publishedDate)
            AddEvent(publishedDate, EventStage.Announced, "Оголошення про проведення закупівлі", "Дата з поля date відповіді API");

        if (ToDate(tender.EnquiryPeriod?.EndDate) is { } enquiryEnd)
            AddEvent(enquiryEnd, EventStage.ClarificationDeadline, "Кінцевий строк звернення за роз'ясненнями");

        var tenderPeriodEnd = ToDate(tender.TenderPeriod?.EndDate);

        if (method != ProcurementMethod.NoElectronicSystem && tenderPeriodEnd is not null)
        {
            AddEvent(tenderPeriodEnd, EventStage.BidsDeadline, "Кінцевий строк подання тендерних пропозицій");

            if (bids.Count > 0)
                AddEvent(tenderPeriodEnd, EventStage.BidsOpened, "Розкриття пропозицій");
        }

        foreach (var award in awards)
        {
            var supplierName = ToOrganization(award.Suppliers?.FirstOrDefault())?.Name;
            var awardDate = ToDate(award.Date);

            if (award.Status == "active" && supplierName is not null)
                AddEvent(awardDate, EventStage.AwardNotice, $"Визначено переможця: {supplierName}",
                    awardDate is null ? "Дату на сторінці закупівлі не вказано" : null);
            else if (award.Status == "unsuccessful" && supplierName is not null)
                AddEvent(awardDate, EventStage.BidRejected, $"Пропозицію учасника {supplierName} відхилено");
        }

        if (contract is not null && contractAmount is not null)
        {
            var signedDate = ToDate(contract.DateSigned);
            var contractNumber = Clean(contract.ContractID) ?? Clean(contract.Id) ?? "без номера";

            AddEvent(
                signedDate ?? ToDate(contract.Date),
                EventStage.ContractSigned,
                $"Укладено договір {contractNumber} на суму {contractAmount.Value.ToString("0.00", CultureInfo.InvariantCulture)} {contractAmount.Currency}",
                signedDate is null ? "Дата з поля date договору; дата підписання в API не вказана" : null);
        }

        if (status == ProcurementStatus.NotHeld)
            AddEvent(tenderPeriodEnd, EventStage.Cancelled, "Закупівлю завершено без укладення договору (статус unsuccessful)");

        var orderedEvents = events
            .Select((e, index) => (e, index))
            .OrderBy(x => x.e.Date is null)
            .ThenBy(x => x.e.Date)
            .ThenBy(x => x.index)
            .Select(x => x.e)
            .ToList();

        // ---- стандартні прогалини
        missing.Add("Джерело фінансування й ідентифікатор плану закупівлі у відповіді API тендера відсутні (вони є в плані закупівлі).");

        if (status == ProcurementStatus.ContractSigned)
            missing.Add("Відомостей про фактичне виконання (постачання, надання послуг, роботи) не знайдено в перевірених джерелах.");

        return new ProzorroImportPlan(
            ExternalId: externalId!,
            ApiId: tender.Id,
            Title: title!,
            Kind: kind!.Value,
            Cpv: cpv,
            Method: method,
            Buyer: buyer!,
            Contractor: contractor,
            Bidders: bidders.Values.ToList(),
            ExpectedAmount: ToMoney(tender.Value),
            ContractAmount: contractAmount,
            Status: status,
            SourceStatus: sourceStatus,
            Place: place,
            PeriodStart: ToDate(item?.DeliveryDate?.StartDate),
            PeriodEnd: ToDate(item?.DeliveryDate?.EndDate),
            NonCompetitiveBasis: basis,
            BidsListComplete: bidsComplete,
            Bids: bids,
            Events: orderedEvents,
            Funding: [],
            Notes: [$"Імпортовано з Prozorro API {importedOn:yyyy-MM-dd}."],
            MissingData: missing,
            NeedsCheck: needsCheck,
            Warnings: warnings,
            SourceUrl: sourceUrl);
    }

    // ---------- допоміжні

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsCpvScheme(string? scheme)
        => scheme is null
           || string.Equals(scheme, "ДК021", StringComparison.OrdinalIgnoreCase)
           || string.Equals(scheme, "CPV", StringComparison.OrdinalIgnoreCase);

    private static DateOnly? ToDate(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateOnly.FromDateTime(parsed.DateTime)
            : null;

    private static MoneyAmount? ToMoney(ProzorroValue? value)
        => value?.Amount is { } amount
            ? new MoneyAmount(amount, value.ValueAddedTaxIncluded, Clean(value.Currency) ?? "UAH")
            : null;

    private static string? FormatAddress(ProzorroAddress? address)
    {
        if (address is null)
            return null;

        var parts = new[] { address.PostalCode, address.CountryName, address.Region, address.Locality, address.StreetAddress }
            .Select(Clean)
            .Where(part => part is not null);

        var text = string.Join(", ", parts);

        return text.Length == 0 ? null : text;
    }

    private static ImportedOrganization? ToOrganization(ProzorroOrganization? organization)
    {
        var edrpou = Clean(organization?.Identifier?.Id);
        var scheme = Clean(organization?.Identifier?.Scheme);

        if (edrpou is null || (scheme is not null && !string.Equals(scheme, EdrScheme, StringComparison.OrdinalIgnoreCase)))
            return null;

        var name = Clean(organization?.Identifier?.LegalName) ?? Clean(organization?.Name);

        return name is null ? null : new ImportedOrganization(name, edrpou, FormatAddress(organization?.Address));
    }
}