using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class ProcurementNoteConfiguration : IEntityTypeConfiguration<ProcurementNote>
{
    public void Configure(EntityTypeBuilder<ProcurementNote> builder)
    {
        builder.ToTable("ProcurementNotes");

        builder.ConfigureEntity();

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.SourceUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(x => x.ProcurementId);
    }
}