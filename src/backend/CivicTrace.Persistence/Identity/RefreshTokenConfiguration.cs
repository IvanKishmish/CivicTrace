using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.Identity;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        RelationalEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)builder, "RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .IsRequired();

        // identifies the chain of tokens a single login produced through rotation
        builder.Property(x => x.FamilyId)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        // revoking an entire family at once on reuse detection
        builder.HasIndex(x => x.FamilyId);
    }
}