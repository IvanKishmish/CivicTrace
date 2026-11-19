using CivicTrace.Domain.Common;

namespace CivicTrace.Domain.Entities;

public sealed class RentalAuction : Entity
{
    public string AuctionId { get; private set; } = string.Empty;
    public string ApiId { get; private set; } = string.Empty;
    public Guid? RentalObjectId { get; private set; }
    public string? RegistryObjectOcid { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string SellingMethod { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string StatusLabel { get; private set; } = string.Empty;
    public int? TenderAttempts { get; private set; }

    public decimal? StartPrice { get; private set; }
    public decimal? FinalPrice { get; private set; }
    public decimal? MinimalStep { get; private set; }
    public decimal? AreaSqm { get; private set; }
    public decimal? PricePerSqmStart { get; private set; }
    public decimal? PricePerSqmFinal { get; private set; }

    public int BidsCount { get; private set; }
    public bool HasSingleBidder { get; private set; }
    public decimal? PriceIncreasePct { get; private set; }
    public string? WinnerKeyMasked { get; private set; }

    public DateTimeOffset? DatePublished { get; private set; }
    public DateTimeOffset? AuctionDate { get; private set; }
    public DateTimeOffset? DateCompleted { get; private set; }
    public DateTimeOffset? ContractDateSigned { get; private set; }

    public string SourceUrl { get; private set; } = string.Empty;
    public int RiskScore { get; private set; }
    public string RiskFlagsJson { get; private set; } = "[]";

    private RentalAuction() { }

    public static RentalAuction Create(
        string auctionId,
        string apiId,
        Guid? rentalObjectId,
        string? registryObjectOcid,
        string title,
        string sellingMethod,
        string status,
        string statusLabel,
        int? tenderAttempts,
        decimal? startPrice,
        decimal? finalPrice,
        decimal? minimalStep,
        decimal? areaSqm,
        int bidsCount,
        string? winnerKeyMasked,
        DateTimeOffset? datePublished,
        DateTimeOffset? auctionDate,
        DateTimeOffset? dateCompleted,
        DateTimeOffset? contractDateSigned)
    {
        decimal? perSqmStart = areaSqm > 0 && startPrice is not null ? Math.Round(startPrice.Value / areaSqm.Value, 2) : null;
        decimal? perSqmFinal = areaSqm > 0 && finalPrice is not null ? Math.Round(finalPrice.Value / areaSqm.Value, 2) : null;
        decimal? incPct = startPrice > 0 && finalPrice is not null
            ? Math.Round((finalPrice.Value - startPrice.Value) / startPrice.Value * 100m, 2)
            : null;

        return new RentalAuction
        {
            Id = Guid.CreateVersion7(),
            AuctionId = auctionId,
            ApiId = apiId,
            RentalObjectId = rentalObjectId,
            RegistryObjectOcid = registryObjectOcid,
            Title = title,
            SellingMethod = sellingMethod,
            Status = status,
            StatusLabel = statusLabel,
            TenderAttempts = tenderAttempts,
            StartPrice = startPrice,
            FinalPrice = finalPrice,
            MinimalStep = minimalStep,
            AreaSqm = areaSqm,
            PricePerSqmStart = perSqmStart,
            PricePerSqmFinal = perSqmFinal,
            BidsCount = bidsCount,
            HasSingleBidder = bidsCount == 1 && status == "complete",
            PriceIncreasePct = incPct,
            WinnerKeyMasked = winnerKeyMasked,
            DatePublished = datePublished,
            AuctionDate = auctionDate,
            DateCompleted = dateCompleted,
            ContractDateSigned = contractDateSigned,
            SourceUrl = $"https://prozorro.sale/auction/{auctionId}/"
        };
    }

    public void UpdateRisk(int score, string flagsJson)
    {
        RiskScore = score;
        RiskFlagsJson = flagsJson;
    }

    public void AttachRentalObject(Guid rentalObjectId)
    {
        RentalObjectId = rentalObjectId;
    }

    public void RefreshFrom(RentalAuction snapshot)
    {
        ApiId = snapshot.ApiId;
        RentalObjectId = snapshot.RentalObjectId ?? RentalObjectId;
        RegistryObjectOcid = snapshot.RegistryObjectOcid ?? RegistryObjectOcid;
        Title = snapshot.Title;
        SellingMethod = snapshot.SellingMethod;
        Status = snapshot.Status;
        StatusLabel = snapshot.StatusLabel;
        TenderAttempts = snapshot.TenderAttempts;
        StartPrice = snapshot.StartPrice;
        FinalPrice = snapshot.FinalPrice;
        MinimalStep = snapshot.MinimalStep;
        AreaSqm = snapshot.AreaSqm;
        PricePerSqmStart = snapshot.PricePerSqmStart;
        PricePerSqmFinal = snapshot.PricePerSqmFinal;
        BidsCount = snapshot.BidsCount;
        HasSingleBidder = snapshot.HasSingleBidder;
        PriceIncreasePct = snapshot.PriceIncreasePct;
        WinnerKeyMasked = snapshot.WinnerKeyMasked;
        DatePublished = snapshot.DatePublished;
        AuctionDate = snapshot.AuctionDate;
        DateCompleted = snapshot.DateCompleted;
        ContractDateSigned = snapshot.ContractDateSigned;
        SourceUrl = snapshot.SourceUrl;
    }
}
