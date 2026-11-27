namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>Orders of the storefront product list (Workflow.md §9). Ties are broken by id, so paging is stable.</summary>
public enum ProductSort
{
    /// <summary>Newest first; the default.</summary>
    Newest,

    /// <summary>Lowest min price first.</summary>
    PriceAsc,

    /// <summary>Highest min price first.</summary>
    PriceDesc,
}
