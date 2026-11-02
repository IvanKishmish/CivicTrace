using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class DataGapConfiguration : IEntityTypeConfiguration<DataGap>
{
    public void Configure(EntityTypeBuilder<DataGap> builder)
    {
        builder.ToTable("DataGaps");

        builder.ConfigureEntity();

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasIndex(x => x.ProcurementId);
    }
}