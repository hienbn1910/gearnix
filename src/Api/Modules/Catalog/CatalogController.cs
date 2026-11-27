using Gearnix.Api.Common.Errors;
using Gearnix.Api.Common.Paging;
using Gearnix.Api.Modules.Catalog.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>
/// The storefront catalog: read-only and open to everyone, signed in or not (Workflow.md §9).
/// Only visible products are shown; a hidden product looks exactly like one that does not exist.
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public sealed class CatalogController(CategoryService categoryService, ProductService productService) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType<CategoryTree>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        Ok(await categoryService.GetTreeAsync(cancellationToken));

    // Not named "page": a query key equal to the parameter name would make MVC bind "page.page" instead of "page".
    [HttpGet("products")]
    [ProducesResponseType<PagedResponse<StorefrontProductListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Products(
        [FromQuery] PageRequest paging, [FromQuery] StorefrontProductFilter filter, CancellationToken cancellationToken) =>
        Ok(await productService.ListVisibleAsync(paging, filter, cancellationToken));

    [HttpGet("products/{slug}")]
    [ProducesResponseType<StorefrontProduct>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Product(string slug, CancellationToken cancellationToken)
    {
        var result = await productService.GetVisibleAsync(slug, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
