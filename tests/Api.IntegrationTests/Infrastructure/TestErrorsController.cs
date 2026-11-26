using Microsoft.AspNetCore.Mvc;

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
}
