using Gearnix.Api.Common.Auth;

using Microsoft.AspNetCore.Identity;

namespace Gearnix.Api.Modules.Identity;

/// <summary>
/// Seeds the roles (Workflow.md §13.4). Idempotent: running it twice changes nothing.
/// </summary>
public sealed class IdentitySeeder(RoleManager<IdentityRole<Guid>> roleManager)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var role in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }));
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Seeding failed: " + string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
