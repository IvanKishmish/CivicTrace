using CivicTrace.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicTrace.Persistence.EntityTypeConfigurations;

internal static class EntityConfigurationExtensions
{
    /// <summary>
    /// Спільна частина для всіх сутностей: ключ, Id без генерації в БД (Guid v7 створює домен) і поля аудиту.
    /// У QuizArena ці рядки повторювалися в кожній конфігурації.
    /// </summary>
    public static EntityTypeBuilder<T> ConfigureEntity<T>(this EntityTypeBuilder<T> builder)
        where T : Entity
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy);

        return builder;
    }
}