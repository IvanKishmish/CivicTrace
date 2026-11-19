using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class RentalObjectConfiguration : IEntityTypeConfiguration<RentalObject>
{
    public void Configure(EntityTypeBuilder<RentalObject> builder)
    {
        builder.ToTable("RentalObjects");
        builder.ConfigureEntity();

        builder.Property(x => x.Ocid).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ApiId).HasMaxLength(40);
        builder.Property(x => x.RawTitle).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.NormalizedTitle).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ObjectType).IsRequired().HasMaxLength(150);
        builder.Property(x => x.CanonicalUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.OriginalUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.EnrichmentStatus).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Address).HasMaxLength(1000);
        builder.Property(x => x.District).HasMaxLength(120);
        builder.Property(x => x.CoordinatesStatus).HasMaxLength(30);
        builder.Property(x => x.AreaSqm).HasPrecision(18, 2);
        builder.Property(x => x.StatusCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.StatusLabel).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceStatus).HasMaxLength(100);
        builder.Property(x => x.ListType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.PropertyOwnerName).HasMaxLength(500);
        builder.Property(x => x.PropertyOwnerEdrpou).HasMaxLength(16);
        builder.Property(x => x.SellingEntityName).HasMaxLength(500);
        builder.Property(x => x.SellingEntityEdrpou).HasMaxLength(16);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.SecondListAbsoluteValue).HasPrecision(18, 2);
        builder.Property(x => x.LeaseDuration).HasMaxLength(40);
        builder.Property(x => x.CurrentContractNumber).HasMaxLength(120);
        builder.Property(x => x.CurrentContractMethod).HasMaxLength(80);
        builder.Property(x => x.CurrentContractValue).HasPrecision(18, 2);
        builder.Property(x => x.CurrentTenantLabel).HasMaxLength(500);
        builder.Property(x => x.DataDefectsNotes).HasMaxLength(2000);

        builder.HasMany(x => x.Auctions)
            .WithOne()
            .HasForeignKey(a => a.RentalObjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Navigation(x => x.Auctions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.Ocid).IsUnique();
        builder.HasIndex(x => x.StatusCode);
        builder.HasIndex(x => x.EnrichmentStatus);
        builder.HasIndex(x => x.CreatedYear);
    }
}

public sealed class RentalAuctionConfiguration : IEntityTypeConfiguration<RentalAuction>
{
    public void Configure(EntityTypeBuilder<RentalAuction> builder)
    {
        builder.ToTable("RentalAuctions");
        builder.ConfigureEntity();

        builder.Property(x => x.AuctionId).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ApiId).IsRequired().HasMaxLength(40);
        builder.Property(x => x.RegistryObjectOcid).HasMaxLength(40);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.SellingMethod).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(60);
        builder.Property(x => x.StatusLabel).IsRequired().HasMaxLength(120);
        builder.Property(x => x.StartPrice).HasPrecision(18, 2);
        builder.Property(x => x.FinalPrice).HasPrecision(18, 2);
        builder.Property(x => x.MinimalStep).HasPrecision(18, 2);
        builder.Property(x => x.AreaSqm).HasPrecision(18, 2);
        builder.Property(x => x.PricePerSqmStart).HasPrecision(18, 2);
        builder.Property(x => x.PricePerSqmFinal).HasPrecision(18, 2);
        builder.Property(x => x.PriceIncreasePct).HasPrecision(18, 2);
        builder.Property(x => x.WinnerKeyMasked).HasMaxLength(120);
        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.RiskFlagsJson).IsRequired().HasMaxLength(4000);

        builder.HasIndex(x => x.AuctionId).IsUnique();
        builder.HasIndex(x => x.RentalObjectId);
        builder.HasIndex(x => x.RegistryObjectOcid);
    }
}