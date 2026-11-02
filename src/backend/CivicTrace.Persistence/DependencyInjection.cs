using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Persistence.Context;
using CivicTrace.Persistence.Identity;
using CivicTrace.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConfigurationKeys.DbConnectionString)
                               ?? throw new InvalidOperationException(
                                   $"Connection string '{ConfigurationKeys.DbConnectionString}' not found.");

        services.AddScoped<UpdateAuditableEntitiesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var auditingInterceptor = sp.GetRequiredService<UpdateAuditableEntitiesInterceptor>();

            options.UseNpgsql(connectionString)
                .AddInterceptors(auditingInterceptor);
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddDbContext<ApplicationIdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IIdentityService, IdentityService>();

        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }

    public static IdentityBuilder AddInfrastructureIdentity(this IServiceCollection services)
    {
        return services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();
    }
}