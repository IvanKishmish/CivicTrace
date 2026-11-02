using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class ProcurementConfiguration : IEntityTypeConfiguration<Procurement>
{
    public void Configure(EntityTypeBuilder<Procurement> builder)
    {
        builder.ToTable("Procurements");

        builder.ConfigureEntity();

        builder.Property(x => x.ExternalId)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.ApiId)
            .HasMaxLength(64);

        builder.Property(x => x.PlanExternalId)
            .HasMaxLength(40);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Method)
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.SourceStatus)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SourceUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PeriodStart);
        builder.Property(x => x.PeriodEnd);
        builder.Property(x => x.BidsListComplete);
        builder.Property(x => x.VerifiedAt);

        // Похідне від Status, у БД не зберігається
        builder.Ignore(x => x.ExecutionStatus);

        // ---- value objects (owned, ті самі колонки в таблиці Procurements)
        builder.OwnsOne(x => x.Cpv, cpv =>
        {
            cpv.Property(p => p.Code)
                .HasColumnName("CpvCode")
                .IsRequired()
                .HasMaxLength(20);

            cpv.Property(p => p.Name)
                .HasColumnName("CpvName")
                .IsRequired()
                .HasMaxLength(300);

            cpv.HasIndex(p => p.Code);
        });
        builder.Navigation(x => x.Cpv).IsRequired();

        builder.OwnsOne(x => x.Place, place =>
        {
            place.Property(p => p.Address)
                .HasColumnName("PlaceAddress")
                .HasMaxLength(500);

            place.Property(p => p.Type)
                .HasColumnName("PlaceType")
                .HasConversion<string>()
                .HasMaxLength(20);

            place.Property(p => p.Latitude)
                .HasColumnName("PlaceLatitude");

            place.Property(p => p.Longitude)
                .HasColumnName("PlaceLongitude");

            place.Property(p => p.CoordinatesStatus)
                .HasColumnName("PlaceCoordinatesStatus")
                .HasConversion<string>()
                .HasMaxLength(20);
        });
        builder.Navigation(x => x.Place).IsRequired();

        builder.OwnsOne(x => x.ExpectedAmount, money =>
        {
            money.Property(p => p.Value)
                .HasColumnName("ExpectedAmount")
                .HasPrecision(18, 2);

            money.Property(p => p.VatIncluded)
                .HasColumnName("ExpectedAmountVatIncluded");

            money.Property(p => p.Currency)
                .HasColumnName("ExpectedAmountCurrency")
                .HasMaxLength(3);
        });

        builder.OwnsOne(x => x.ContractAmount, money =>
        {
            money.Property(p => p.Value)
                .HasColumnName("ContractAmount")
                .HasPrecision(18, 2);

            money.Property(p => p.VatIncluded)
                .HasColumnName("ContractAmountVatIncluded");

            money.Property(p => p.Currency)
                .HasColumnName("ContractAmountCurrency")
                .HasMaxLength(3);
        });

        builder.OwnsOne(x => x.NonCompetitiveBasis, basis =>
        {
            basis.Property(p => p.LegalBasis)
                .HasColumnName("NonCompetitiveLegalBasis")
                .HasMaxLength(500);

            basis.Property(p => p.Justification)
                .HasColumnName("NonCompetitiveJustification")
                .HasMaxLength(1000);

            basis.Property(p => p.SourceUrl)
                .HasColumnName("NonCompetitiveSourceUrl")
                .HasMaxLength(500);
        });

        // ---- зв'язки з іншими агрегатами
        builder.HasOne(x => x.Buyer)
            .WithMany()
            .HasForeignKey(x => x.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contractor)
            .WithMany()
            .HasForeignKey(x => x.ContractorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(x => x.VerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- дочірні сутності агрегату (видаляються разом із закупівлею)
        builder.HasMany(x => x.Events)
            .WithOne()
            .HasForeignKey(x => x.ProcurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Bids)
            .WithOne()
            .HasForeignKey(x => x.ProcurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.FundingSources)
            .WithOne()
            .HasForeignKey(x => x.ProcurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Notes)
            .WithOne()
            .HasForeignKey(x => x.ProcurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.DataGaps)
            .WithOne()
            .HasForeignKey(x => x.ProcurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Bids).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.FundingSources).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.DataGaps).UsePropertyAccessMode(PropertyAccessMode.Field);

        // ---- індекси
        builder.HasIndex(x => x.ExternalId)
            .IsUnique();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Method);
        builder.HasIndex(x => x.Kind);
        builder.HasIndex(x => x.BuyerId);
        builder.HasIndex(x => x.ContractorId);
    }
}