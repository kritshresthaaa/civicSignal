using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CivicSignal.Infrastructure.Identity;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedDevelopmentIdentityAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var options = configuration
            .GetSection(DevelopmentIdentitySeedOptions.SectionName)
            .Get<DevelopmentIdentitySeedOptions>() ?? new DevelopmentIdentitySeedOptions();

        if (!options.Enabled)
        {
            return;
        }

        try
        {
            using var scope = services.CreateScope();
            var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

            if (options.DevelopmentOnly && !environment.IsDevelopment())
            {
                logger.LogInformation("Skipping identity seed outside Development environment.");
                return;
            }

            if (options.Users.Length == 0)
            {
                logger.LogInformation("Identity seed is enabled, but no users are configured.");
                return;
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            foreach (var seedUser in options.Users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SeedUserAsync(seedUser, userManager, roleManager);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Development identity seed was skipped. Ensure PostgreSQL is running and migrations are applied.");
        }
    }

    private static async Task SeedUserAsync(
        DevelopmentSeedUserOptions seedUser,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        if (string.IsNullOrWhiteSpace(seedUser.Email)
            || string.IsNullOrWhiteSpace(seedUser.Password))
        {
            return;
        }

        var email = seedUser.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = NormalizeDisplayName(seedUser.DisplayName),
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, seedUser.Password);
            ThrowIfFailed(createResult, $"create seed user '{email}'");
        }
        else
        {
            user.EmailConfirmed = true;
            user.DisplayName = NormalizeDisplayName(seedUser.DisplayName) ?? user.DisplayName;

            var updateResult = await userManager.UpdateAsync(user);
            ThrowIfFailed(updateResult, $"update seed user '{email}'");
        }

        if (seedUser.ResetPassword && !await userManager.CheckPasswordAsync(user, seedUser.Password))
        {
            if (await userManager.HasPasswordAsync(user))
            {
                var removePasswordResult = await userManager.RemovePasswordAsync(user);
                ThrowIfFailed(removePasswordResult, $"remove seed user '{email}' password");
            }

            var addPasswordResult = await userManager.AddPasswordAsync(user, seedUser.Password);
            ThrowIfFailed(addPasswordResult, $"set seed user '{email}' password");
        }

        foreach (var roleName in seedUser.Roles.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).Distinct())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = roleName
                });
                ThrowIfFailed(roleResult, $"create seed role '{roleName}'");
            }

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
                ThrowIfFailed(addRoleResult, $"add seed user '{email}' to role '{roleName}'");
            }
        }
    }

    private static string? NormalizeDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Could not {operation}: {errors}");
    }
}
