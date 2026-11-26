using Gearnix.Api.Data;
using Gearnix.Api.Modules.Identity.Entities;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Gearnix.Api.Modules.Identity;

/// <summary>ASP.NET Core Identity with cookie authentication (Workflow.md §11.5, ADR 0004).</summary>
public static class IdentitySetup
{
    public const string AuthCookieName = "gearnix.auth";

    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, cookie =>
            {
                cookie.Cookie.Name = AuthCookieName;
                cookie.Cookie.HttpOnly = true; // scripts cannot read it
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only (browsers allow localhost)
                cookie.Cookie.SameSite = SameSiteMode.Strict; // never sent on cross-site requests
                cookie.ExpireTimeSpan = TimeSpan.FromDays(7);
                cookie.SlidingExpiration = true;

                // An API answers 401/403, never a redirect to a login page. UseStatusCodePages adds the ProblemDetails body.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddIdentityCore<AppUser>(options =>
            {
                // Length over complexity (NIST SP 800-63B): at least 8 characters, no forced character classes.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        // Keys that encrypt the auth cookie live in the database, so a redeployed container keeps users signed in.
        services.AddDataProtection()
            .SetApplicationName("Gearnix")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
