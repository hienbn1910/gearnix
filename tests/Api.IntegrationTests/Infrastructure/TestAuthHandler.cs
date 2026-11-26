using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearnix.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only authentication (Workflow.md §13.2): the "X-Test-User: {id};{role}" header selects the user, so tests
/// can act as any role without hashing passwords. Registered only by <see cref="GearnixApiFactory"/>.
/// Claims match what the real Identity cookie carries (NameIdentifier, Role).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string HeaderName = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var parts = Request.Headers[HeaderName].ToString().Split(';');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Expected {HeaderName}: {{id}};{{role}}."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, parts[1])],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
