namespace CivicTrace.Application.Features.Prozorro.Models;

// Мінімальна форма відповіді GET /tenders/{id} публічного API Prozorro (назви полів за документацією
// prozorro-api-docs.readthedocs.io). Усе необов'язкове: відсутні поля не мають ламати розбір.
// Дати лишаються рядками й розбираються у ProzorroTenderMapper.

public sealed class ProzorroTenderEnvelope
{
    public ProzorroTender? Data { get; init; }
}

public sealed class ProzorroTender
{
    public string Id { get; init; } = string.Empty;
    public string? TenderID { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? ProcurementMethod { get; init; }
    public string? ProcurementMethodType { get; init; }
    public string? MainProcurementCategory { get; init; }
    public string? Date { get; init; }
    public ProzorroValue? Value { get; init; }
    public ProzorroOrganization? ProcuringEntity { get; init; }
    public List<ProzorroItem>? Items { get; init; }
    public List<ProzorroAward>? Awards { get; init; }
    public List<ProzorroContract>? Contracts { get; init; }
    public List<ProzorroBid>? Bids { get; init; }
    public ProzorroCauseDetails? CauseDetails { get; init; }
    public ProzorroPeriod? TenderPeriod { get; init; }
    public ProzorroPeriod? EnquiryPeriod { get; init; }
}

public sealed class ProzorroValue
{
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public bool? ValueAddedTaxIncluded { get; init; }
}

public sealed class ProzorroPeriod
{
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
}

public sealed class ProzorroIdentifier
{
    public string? Scheme { get; init; }
    public string? Id { get; init; }
    public string? LegalName { get; init; }
}

public sealed class ProzorroAddress
{
    public string? StreetAddress { get; init; }
    public string? Locality { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryName { get; init; }
}

public sealed class ProzorroOrganization
{
    public string? Name { get; init; }
    public ProzorroIdentifier? Identifier { get; init; }
    public ProzorroAddress? Address { get; init; }
}

public sealed class ProzorroClassification
{
    public string? Scheme { get; init; }
    public string? Id { get; init; }
    public string? Description { get; init; }
}

public sealed class ProzorroLocation
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

public sealed class ProzorroItem
{
    public string? Description { get; init; }
    public ProzorroClassification? Classification { get; init; }
    public ProzorroAddress? DeliveryAddress { get; init; }
    public ProzorroLocation? DeliveryLocation { get; init; }
    public ProzorroPeriod? DeliveryDate { get; init; }
}

public sealed class ProzorroAward
{
    public string? Id { get; init; }
    public string? Status { get; init; }
    public string? Date { get; init; }
    public List<ProzorroOrganization>? Suppliers { get; init; }
    public ProzorroValue? Value { get; init; }
}

public sealed class ProzorroContract
{
    public string? Id { get; init; }
    public string? ContractID { get; init; }
    public string? AwardID { get; init; }
    public string? Status { get; init; }
    public string? DateSigned { get; init; }
    public string? Date { get; init; }
    public ProzorroValue? Value { get; init; }
}

public sealed class ProzorroBid
{
    public string? Id { get; init; }
    public string? Status { get; init; }
    public List<ProzorroOrganization>? Tenderers { get; init; }
    public ProzorroValue? Value { get; init; }
}

public sealed class ProzorroCauseDetails
{
    public string? Code { get; init; }
    public string? Scheme { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
}