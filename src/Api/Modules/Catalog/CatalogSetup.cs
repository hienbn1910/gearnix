namespace Gearnix.Api.Modules.Catalog;

public static class CatalogSetup
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<CategoryRepository>();
        services.AddScoped<CategoryService>();
        return services;
    }
}
