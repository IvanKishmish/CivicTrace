using CivicTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");

        builder.ConfigureEntity();

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(100);
    }
}