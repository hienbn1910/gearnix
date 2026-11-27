using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>Filters of the admin product list, bound from the query string (?q=...&amp;categoryId=...&amp;isActive=...).</summary>
public sealed class ProductListFilter
{
    /// <summary>Text the product name must contain; case and Vietnamese diacritics are ignored.</summary>
    /// <remarks>
    /// Bound by its explicit name, so a validation error is reported under "q" (the key the client sent):
    /// errors are keyed by the bound name, and camelCase leaves the one-letter name "Q" upper case.
    /// </remarks>
    [FromQuery(Name = "q")]
    public string? Q { get; init; }

    public Guid? CategoryId { get; init; }

    public bool? IsActive { get; init; }
}
