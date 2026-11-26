namespace Gearnix.Api.Common.Auth;

public static class AuthorizationSetup
{
    public static IServiceCollection AddGearnixAuthorization(this IServiceCollection services)
    {
        // Secure by default is applied in Program.cs with MapControllers().RequireAuthorization(), not with a
        // fallback policy: a fallback policy also covers unmatched routes, turning an anonymous 404 into a 401.
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Customer, policy => policy.RequireRole(Roles.Customer))
            .AddPolicy(Policies.Staff, policy => policy.RequireRole(Roles.WarehouseStaff, Roles.Admin))
            .AddPolicy(Policies.WarehouseStaff, policy => policy.RequireRole(Roles.WarehouseStaff))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));
        return services;
    }
}
