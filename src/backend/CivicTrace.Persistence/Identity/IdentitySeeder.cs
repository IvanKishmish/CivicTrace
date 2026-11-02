using CivicTrace.Application.Common;
using CivicTrace.Domain.Entities;
using CivicTrace.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence.Identity;

public static class IdentitySeeder
{
    /// <summary>
    /// Створює ролі з Roles.All. Якщо в конфігурації задано Seed:Admin:Email і Seed:Admin:Password,
    /// створює адміністратора та його UserProfile. Безпечно викликати повторно.
    /// </summary>
    public static async Task SeedIdentityAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var configuration = sp.GetRequiredService<IConfiguration>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var appDb = sp.GetRequiredService<AppDbContext>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var email = configuration[ConfigurationKeys.SeedAdminEmail];
        var password = configuration[ConfigurationKeys.SeedAdminPassword];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var admin = await userManager.FindByEmailAsync(email);

        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email };

            var created = await userManager.CreateAsync(admin, password);

            if (!created.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create the seed admin: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
            await userManager.AddToRoleAsync(admin, Roles.Admin);

        if (!await appDb.UserProfiles.AnyAsync(p => p.Id == admin.Id, ct))
        {
            var profile = UserProfile.Create(admin.Id, "Admin");

            if (profile.IsError)
                throw new InvalidOperationException($"Could not create the admin profile: {profile.FirstError.Description}");

            appDb.UserProfiles.Add(profile.Value);
            await appDb.SaveChangesAsync(ct);
        }
    }
}