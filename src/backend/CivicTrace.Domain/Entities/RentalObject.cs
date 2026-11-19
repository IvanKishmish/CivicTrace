using CivicTrace.Domain.Common;

namespace CivicTrace.Domain.Entities;

public sealed class RentalObject : Entity
{
    private readonly List<RentalAuction> _auctions = [];

    public string Ocid { get; private set; } = string.Empty;
    public string? ApiId { get; private set; }
    public string RawTitle { get; private set; } = string.Empty;
    public string NormalizedTitle { get; private set; } = string.Empty;
    public string ObjectType { get; private set; } = string.Empty;
    public string CanonicalUrl { get; private set; } = string.Empty;
    public string OriginalUrl { get; private set; } = string.Empty;

    /// <summary>NotEnriched | Enriched | NotFoundInApi</summary>
    public string EnrichmentStatus { get; private set; } = "NotEnriched";

    public int? CreatedYear { get; private set; }
    public DateTimeOffset? DatePublished { get; private set; }
    public DateTimeOffset? DateModified { get; private set; }

    public string? Address { get; private set; }
    public string? District { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    /// <summary>VerifiedFromApi | NeedsCheck | null</summary>
    public string? CoordinatesStatus { get; private set; }

    public decimal? AreaSqm { get; private set; }

    /// <summary>rented | free | on_auction | unknown</summary>
    public string StatusCode { get; private set; } = "unknown";
    public string StatusLabel { get; private set; } = "Не збагачено";
    public string? SourceStatus { get; private set; }

    /// <summary>first | second | unknown</summary>
    public string ListType { get; private set; } = "unknown";

    public string? PropertyOwnerName { get; private set; }
    public string? PropertyOwnerEdrpou { get; private set; }
    public string? SellingEntityName { get; private set; }
    public string? SellingEntityEdrpou { get; private set; }

    public decimal? MarketValue { get; private set; }
    public decimal? SecondListAbsoluteValue { get; private set; }
    public string? LeaseDuration { get; private set; }
    public bool? SubleaseAllowed { get; private set; }

    public string? CurrentContractNumber { get; private set; }
    public string? CurrentContractMethod { get; private set; }
    public decimal? CurrentContractValue { get; private set; }
    public DateTimeOffset? CurrentContractStart { get; private set; }
    public DateTimeOffset? CurrentContractEnd { get; private set; }
    public string? CurrentTenantLabel { get; private set; }

    public string? DataDefectsNotes { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    public IReadOnlyCollection<RentalAuction> Auctions => _auctions.AsReadOnly();

    private RentalObject() { }

    public static RentalObject CreateFromList(
        string ocid,
        string rawTitle,
        string normalizedTitle,
        string objectType,
        string originalUrl,
        int? createdYear,
        string? defects)
    {
        var cleanOcid = ocid.Trim();
        return new RentalObject
        {
            Id = Guid.CreateVersion7(),
            Ocid = cleanOcid,
            RawTitle = rawTitle,
            NormalizedTitle = normalizedTitle,
            ObjectType = objectType,
            OriginalUrl = originalUrl.Trim(),
            CanonicalUrl = $"https://prozorro.sale/planning/{cleanOcid}/",
            EnrichmentStatus = "NotEnriched",
            CreatedYear = createdYear,
            StatusCode = "unknown",
            StatusLabel = "Потребує збагачення",
            ListType = "unknown",
            DataDefectsNotes = string.IsNullOrWhiteSpace(defects) ? null : defects
        };
    }

    public void ApplyEnrichment(
        string? apiId,
        string? titleFromApi,
        DateTimeOffset? datePublished,
        DateTimeOffset? dateModified,
        string? address,
        string? district,
        double? latitude,
        double? longitude,
        string? coordinatesStatus,
        decimal? areaSqm,
        string statusCode,
        string statusLabel,
        string? sourceStatus,
        string listType,
        string? ownerName,
        string? ownerEdrpou,
        string? sellerName,
        string? sellerEdrpou,
        decimal? marketValue,
        decimal? secondListValue,
        string? leaseDuration,
        bool? subleaseAllowed,
        string? contractNumber,
        string? contractMethod,
        decimal? contractValue,
        DateTimeOffset? contractStart,
        DateTimeOffset? contractEnd,
        string? tenantLabel)
    {
        ApiId = apiId;
        if (!string.IsNullOrWhiteSpace(titleFromApi))
            NormalizedTitle = titleFromApi.Trim();

        DatePublished = datePublished;
        DateModified = dateModified;
        Address = address;
        District = district;

        if (latitude is not null && longitude is not null)
        {
            Latitude = latitude;
            Longitude = longitude;
            CoordinatesStatus = coordinatesStatus;
        }

        AreaSqm = areaSqm;
        StatusCode = statusCode;
        StatusLabel = statusLabel;
        SourceStatus = sourceStatus;
        ListType = listType;
        PropertyOwnerName = ownerName;
        PropertyOwnerEdrpou = ownerEdrpou;
        SellingEntityName = sellerName;
        SellingEntityEdrpou = sellerEdrpou;
        MarketValue = marketValue;
        SecondListAbsoluteValue = secondListValue;
        LeaseDuration = leaseDuration;
        SubleaseAllowed = subleaseAllowed;
        CurrentContractNumber = contractNumber;
        CurrentContractMethod = contractMethod;
        CurrentContractValue = contractValue;
        CurrentContractStart = contractStart;
        CurrentContractEnd = contractEnd;
        CurrentTenantLabel = tenantLabel;
        EnrichmentStatus = "Enriched";
        LastSyncedAt = DateTimeOffset.UtcNow;
    }

    public void SetGeocodedCoordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        CoordinatesStatus = "NeedsCheck";
    }

    public void MarkNotFoundInApi()
    {
        if (EnrichmentStatus == "NotEnriched")
            EnrichmentStatus = "NotFoundInApi";
        LastSyncedAt = DateTimeOffset.UtcNow;
    }
}