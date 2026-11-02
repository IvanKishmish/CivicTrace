using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("Bids");

        builder.ConfigureEntity();

        builder.OwnsOne(x => x.Amount, money =>
        {
            money.Property(p => p.Value)
                .HasColumnName("Amount")
                .HasPrecision(18, 2);

            money.Property(p => p.VatIncluded)
                .HasColumnName("AmountVatIncluded");

            money.Property(p => p.Currency)
                .HasColumnName("AmountCurrency")
                .HasMaxLength(3);
        });
        builder.Navigation(x => x.Amount).IsRequired();

        builder.Property(x => x.Outcome)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Bidder)
            .WithMany()
            .HasForeignKey(x => x.BidderId)
            //if we delete a bid, we don't touch the organization
            .OnDelete(DeleteBehavior.Restrict);

        // Зв'язок Procurement -> Bids налаштовано в ProcurementConfiguration

        builder.HasIndex(x => x.BidderId);

        // Один учасник — одна пропозиція в закупівлі (те саме правило є в домені)
        builder.HasIndex(x => new { x.ProcurementId, x.BidderId })
            .IsUnique();
    }
}