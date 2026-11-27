using Gearnix.Api.Common.Auth;
using Gearnix.Api.Common.Errors;
using Gearnix.Api.Common.Paging;
using Gearnix.Api.Modules.Catalog.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.Modules.Catalog;

[ApiController]
[Route("api/admin/products")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AdminProductsController(ProductService productService) : ControllerBase
{
    // Parameter names differ from the query keys on purpose: a key equal to the parameter name ("page") would make
    // MVC look for "page.page" instead of "page".
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PageRequest paging, [FromQuery] ProductListFilter filter, CancellationToken cancellationToken) =>
        Ok(await productService.ListAsync(paging, filter, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await productService.GetAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await productService.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/admin/products/{result.Value.Id}", result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await productService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("{id:guid}/skus")]
    [ProducesResponseType<SkuResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSku(Guid id, SkuRequest request, CancellationToken cancellationToken)
    {
        var result = await productService.CreateSkuAsync(id, request, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/admin/products/{id}/skus/{result.Value.Id}", result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpPut("{id:guid}/skus/{skuId:guid}")]
    [ProducesResponseType<SkuResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSku(
        Guid id, Guid skuId, SkuRequest request, CancellationToken cancellationToken)
    {
        var result = await productService.UpdateSkuAsync(id, skuId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
