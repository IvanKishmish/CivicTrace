using System.Text.Json.Serialization;

namespace CivicTrace.Application.Features.Rentals.Models;

public sealed class SaleLocalizedText
{
    [JsonPropertyName("uk_UA")]
    public string? UkUa { get; init; }
}

public sealed class SaleIdentifier
{
    public string? Scheme { get; init; }
    public string? Id { get; init; }
    public SaleLocalizedText? LegalName { get; init; }
}

public sealed class SaleAddress
{
    public SaleLocalizedText? StreetAddress { get; init; }
    public SaleLocalizedText? Locality { get; init; }
    public SaleLocalizedText? Region { get; init; }
    public string? PostalCode { get; init; }
}

public sealed class SaleLocation
{
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }
}

public sealed class SaleOrganization
{
    public SaleLocalizedText? Name { get; init; }
    public SaleIdentifier? Identifier { get; init; }
    public SaleAddress? Address { get; init; }
}

public sealed class SaleRelatedOrganizations
{
    public string? OwnershipType { get; init; }
    public SaleOrganization? PropertyOwner { get; init; }
    public SaleOrganization? SellingEntity { get; init; }
    public SaleOrganization? Governer { get; init; }
    public List<SaleOrganization>? CurrentTenants { get; init; }
}

public sealed class SaleReProps
{
    public double? TotalObjectArea { get; init; }
    public double? UsableArea { get; init; }
    public string? GeneralCondition { get; init; }
    public string? LocationInBuilding { get; init; }
    public string? ConstructionTechnology { get; init; }
}

public sealed class SalePlacing
{
    public SaleAddress? Address { get; init; }
    public SaleLocation? Location { get; init; }
}

public sealed class SaleRegistryItem
{
    public SalePlacing? Placing { get; init; }
    public SaleReProps? ReProps { get; init; }
}

public sealed class SaleValue
{
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public bool? ValueAddedTaxIncluded { get; init; }
}

public sealed class SaleStatusesDecisions
{
    public string? StatusInList { get; init; }
}

public sealed class SaleLeaseRules
{
    public string? LeaseDuration { get; init; }
    public bool? SubleaseAllowed { get; init; }
}

public sealed class SaleValuesCharacteristics
{
    public SaleValue? MarketValue { get; init; }
    public SaleValue? SecondListAbsoluteValue { get; init; }
}

public sealed class SaleContractTime
{
    public string? DateFrom { get; init; }
    public string? DateTill { get; init; }
}

public sealed class SaleContract
{
    public string? Id { get; init; }
    public string? ContractNumber { get; init; }
    public string? ContractMethod { get; init; }
    public string? Status { get; init; }
    public string? DateSigned { get; init; }
    public SaleValue? Value { get; init; }
    public SaleContractTime? ContractTime { get; init; }
    public List<SaleOrganization>? Buyers { get; init; }
}

public sealed class SaleRegistryObjectDto
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;

    public string? RegistryObjectId { get; init; }
    public string? DatePublished { get; init; }
    public string? DateModified { get; init; }
    public SaleLocalizedText? Title { get; init; }
    public List<SaleRegistryItem>? RegistryObjectItems { get; init; }
    public SaleRelatedOrganizations? RelatedOrganizations { get; init; }
    public SaleStatusesDecisions? StatusesDecisions { get; init; }
    public SaleLeaseRules? LeaseRules { get; init; }
    public SaleValuesCharacteristics? ValuesCharacteristics { get; init; }
    public List<SaleContract>? Contracts { get; init; }
}

public sealed class SaleProcedureItem
{
    public SaleAddress? Address { get; init; }
    public SaleLocation? Location { get; init; }
    public SaleReProps? ReProps { get; init; }
    public double? Quantity { get; init; }
}

public sealed class SaleBid
{
    public string? Id { get; init; }
    public string? Status { get; init; }
    public decimal? InitialValueAmount { get; init; }
    public SaleValue? Value { get; init; }
    public List<SaleOrganization>? Bidders { get; init; }
}

public sealed class SaleAward
{
    public string? Id { get; init; }
    public string? BidId { get; init; }
    public string? Status { get; init; }
    public SaleValue? Value { get; init; }
    public List<SaleOrganization>? Buyers { get; init; }
}

public sealed class SalePeriod
{
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
}

public sealed class SaleProcedureDto
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;

    public string? AuctionId { get; init; }
    public string? LotId { get; init; }
    public string? RegistryId { get; init; }
    public string? RegistryObjectId { get; init; }
    public string? SellingMethod { get; init; }
    public string? Status { get; init; }
    public string? DatePublished { get; init; }
    public string? DateModified { get; init; }
    public string? DateCompleted { get; init; }
    public int? TenderAttempts { get; init; }
    public SaleLocalizedText? Title { get; init; }
    public SaleValue? Value { get; init; }
    public SaleValue? MinimalStep { get; init; }
    public SalePeriod? AuctionPeriod { get; init; }
    public List<SaleProcedureItem>? Items { get; init; }
    public List<SaleBid>? Bids { get; init; }
    public List<SaleAward>? Awards { get; init; }
    public List<SaleContract>? Contracts { get; init; }
    public SaleRelatedOrganizations? RelatedOrganizations { get; init; }
}