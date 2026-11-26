using Gearnix.Api.Common.Errors;
using Gearnix.Api.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only endpoints that trigger each kind of error, so the error pipeline can be tested before real
/// endpoints exist. Added to the API only by <see cref="GearnixApiFactory"/>; production code has no test endpoints.
/// </summary>
[ApiController]
[Route("api/test-errors")]
public sealed class TestErrorsController : ControllerBase
{
    public const string SecretExceptionMessage = "secret internal detail: SELECT * FROM users";

    [HttpGet("unhandled")]
    public IActionResult Unhandled() => throw new InvalidOperationException(SecretExceptionMessage);

    [HttpGet("query-database")]
    public async Task<IActionResult> QueryDatabase([FromServices] AppDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync($"SELECT 1", cancellationToken);
        return Ok();
    }

    [HttpGet("business-conflict")]
    public IActionResult BusinessConflict() => FromResult(new Error("out_of_stock", ErrorKind.Conflict));

    [HttpGet("business-not-found")]
    public IActionResult BusinessNotFound() => FromResult(new Error(ErrorCodes.NotFound, ErrorKind.NotFound));

    // The way a real controller handles a service result.
    private IActionResult FromResult(Result<string> result) =>
        result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);

    // Echoes the body back, so tests can see validation and enum serialization.
    [HttpPost("validated")]
    public IActionResult Validated(TestValidatedRequest request) => Ok(request);
}
