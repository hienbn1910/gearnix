using Gearnix.Api.Common.Auth;
using Gearnix.Api.Common.Hosting;
using Gearnix.Api.Data;
using Gearnix.Api.Modules.Identity.Entities;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Gearnix.Api.Modules.Identity;

/// <summary>
/// Seeds the roles and, in Development and Demo, the demo staff and admin accounts (Workflow.md §13.4).
/// Idempotent: running it twice changes nothing, and an existing account's password is never reset.
/// </summary>
public sealed class IdentitySeeder(
    AppDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    IHostEnvironment environment,
    IOptions<IdentitySeedOptions> options)
{
    public const string StaffEmail = "staff@demo.local";
    public const string AdminEmail = "admin@demo.local";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seedDemoAccounts = environment.HasDemoData();

        // Check configuration before writing anything, so a misconfigured deploy fails clearly and early.
        var staffPassword = seedDemoAccounts ? RequiredPassword(options.Value.StaffPassword, "StaffPassword") : null;
        var adminPassword = seedDemoAccounts ? RequiredPassword(options.Value.AdminPassword, "AdminPassword") : null;

        foreach (var role in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }));
            }
        }

        if (staffPassword is not null && adminPassword is not null)
        {
            await EnsureAccountAsync(StaffEmail, "Nhân viên kho Demo", Roles.WarehouseStaff, staffPassword, cancellationToken);
            await EnsureAccountAsync(AdminEmail, "Quản trị viên Demo", Roles.Admin, adminPassword, cancellationToken);
        }
    }

    private async Task EnsureAccountAsync(
        string email, string fullName, string role, string password, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Already seeded: keep it as it is, including its password. If someone registered this email before
            // the seed ran, it is a Customer; fail loudly instead of leaving the demo without its staff account.
            if (!await userManager.IsInRoleAsync(existing, role))
            {
                throw new InvalidOperationException($"The demo account {email} exists without the {role} role.");
            }

            return;
        }

        var user = new AppUser { UserName = email, Email = email, FullName = fullName };

        // The account and its role are saved together, or not at all.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        EnsureSucceeded(await userManager.CreateAsync(user, password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        await transaction.CommitAsync(cancellationToken);
    }

    private static string RequiredPassword(string? value, string key) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"{IdentitySeedOptions.SectionName}:{key} is required to seed the demo accounts in Development and Demo.")
            : value;

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                // Codes only: descriptions can contain the email (personal data in logs, Rule.md §6).
                "Seeding failed: " + string.Join("; ", result.Errors.Select(error => error.Code)));
        }
    }
}
