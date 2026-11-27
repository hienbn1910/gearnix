namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>
/// The names a client may send as <c>sort</c> (Workflow.md §9). <c>sort</c> is bound as text and looked up here,
/// not bound to the enum directly: MVC's enum binding would also accept numbers ("1") and lists ("a,b").
/// </summary>
public static class ProductSortNames
{
    private static readonly Dictionary<string, ProductSort> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["newest"] = ProductSort.Newest,
        ["priceAsc"] = ProductSort.PriceAsc,
        ["priceDesc"] = ProductSort.PriceDesc,
    };

    public static bool IsKnown(string name) => ByName.ContainsKey(name);

    /// <summary>The order for a validated name; no name means newest first.</summary>
    public static ProductSort Parse(string? name) => name is null ? ProductSort.Newest : ByName[name];
}
