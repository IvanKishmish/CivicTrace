using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class FundingSourceConfiguration : IEntityTypeConfiguration<FundingSource>
{
    public void Configure(EntityTypeBuilder<FundingSource> builder)
    {
        builder.ToTable("FundingSources");

        builder.ConfigureEntity();

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ExpenditureCode)
            .HasMaxLength(500);

        builder.Property(x => x.Territory)
            .HasMaxLength(200);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.HasIndex(x => x.ProcurementId);
    }
}