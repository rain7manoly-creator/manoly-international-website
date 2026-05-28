using Microsoft.AspNetCore.Identity;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Infrastructure.Seeding;

/// <summary>
/// Runs on application startup. Ensures Admin and Worker roles exist
/// and creates a seeded admin account if none exists.
/// </summary>
public static class DbInitializer
{
    public const string AdminRole = "Admin";
    public const string WorkerRole = "Worker";

    public static async Task InitializeAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ILogger logger)
    {
        foreach (var role in new[] { AdminRole, WorkerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Seeded role: {Role}", role);
            }
        }

        var adminUserName = configuration["Seed:AdminUserName"] ?? "admin";
        var adminPassword = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException(
                "Seed:AdminPassword is not configured. Set it via user secrets or environment variables.");
        var adminFullName = configuration["Seed:AdminFullName"] ?? "المسؤول";

        var existing = await userManager.FindByNameAsync(adminUserName);
        if (existing != null)
        {
            logger.LogInformation("Admin account already exists: {UserName}", adminUserName);
            return;
        }

        var admin = ApplicationUser.Create(adminUserName, adminFullName, isAdmin: true);
        var createResult = await userManager.CreateAsync(admin, adminPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create admin: {errors}");
        }

        await userManager.AddToRoleAsync(admin, AdminRole);
        logger.LogInformation("Seeded admin account: {UserName}", adminUserName);
    }
}
