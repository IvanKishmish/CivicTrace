using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.ConfigureEntity();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Edrpou)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Edrpou)
            .IsUnique();
    }
}