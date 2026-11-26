using Microsoft.AspNetCore.Identity;

namespace Gearnix.Api.Modules.Identity.Entities;

/// <summary>
/// A user account (Workflow.md §6.5). UserName is the email; the phone uses Identity's PhoneNumber.
/// Each account has exactly one role (Workflow.md §3).
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public const int FullNameMaxLength = 100;
    public const int CityMaxLength = 100;
    public const int PhoneMaxLength = 20;

    public AppUser()
    {
        // Time-sortable, hard-to-guess key (Workflow.md §6).
        Id = Guid.CreateVersion7();
    }

    public required string FullName { get; set; }

    public string? City { get; set; }
}
