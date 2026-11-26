using Gearnix.Api.Common.Auth;
using Gearnix.Api.Modules.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>Test-only endpoints, one per access level, to test the authorization policies.</summary>
[ApiController]
[Route("api/test-auth")]
public sealed class TestAuthController(ICurrentUser currentUser) : ControllerBase
{
    // No attribute: the secure-by-default rule (signed-in user) applies.
    [HttpGet("signed-in")]
    public IActionResult SignedIn() => Ok(new { id = currentUser.Id, role = currentUser.Role });

    [HttpGet("customer")]
    [Authorize(Policy = Policies.Customer)]
    public IActionResult Customer() => Ok();

    [HttpGet("staff")]
    [Authorize(Policy = Policies.Staff)]
    public IActionResult Staff() => Ok();

    [HttpGet("warehouse-staff")]
    [Authorize(Policy = Policies.WarehouseStaff)]
    public IActionResult WarehouseStaff() => Ok();

    [HttpGet("admin")]
    [Authorize(Policy = Policies.Admin)]
    public IActionResult Admin() => Ok();
}
