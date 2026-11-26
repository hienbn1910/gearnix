using Gearnix.Api.Modules.Identity.Entities;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.Data.Configurations;

/// <summary>
/// Short table names for the ASP.NET Core Identity tables (instead of asp_net_users, …) and the extra user columns.
/// </summary>
public static class IdentityModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.FullName).HasMaxLength(AppUser.FullNameMaxLength);
            user.Property(u => u.City).HasMaxLength(AppUser.CityMaxLength);
            user.Property(u => u.PhoneNumber).HasMaxLength(AppUser.PhoneMaxLength);
        });
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
