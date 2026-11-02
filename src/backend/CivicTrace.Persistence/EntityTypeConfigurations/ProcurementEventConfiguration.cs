using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class ProcurementEventConfiguration : IEntityTypeConfiguration<ProcurementEvent>
{
    public void Configure(EntityTypeBuilder<ProcurementEvent> builder)
    {
        builder.ToTable("ProcurementEvents");

        builder.ConfigureEntity();

        builder.Property(x => x.Date);

        builder.Property(x => x.Stage)
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.DateNote)
            .HasMaxLength(500);

        builder.Property(x => x.SourceUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.HasIndex(x => new { x.ProcurementId, x.SortOrder });
    }
}