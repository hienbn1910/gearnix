namespace Gearnix.Api.Modules.Identity;

/// <summary>
/// Passwords of the demo staff and admin accounts, from the "Seed" configuration section (user-secrets locally,
/// environment variables on the server). Never committed. Required only when seeding Development or Demo.
/// </summary>
public sealed class IdentitySeedOptions
{
    public const string SectionName = "Seed";

    public string? StaffPassword { get; set; }

    public string? AdminPassword { get; set; }
}
