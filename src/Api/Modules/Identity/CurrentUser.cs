using System.Security.Claims;

namespace Gearnix.Api.Modules.Identity;

/// <summary>Reads the current user from the authenticated principal of the HTTP request.</summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid Id => Guid.Parse(RequiredClaim(ClaimTypes.NameIdentifier));

    public string Role => RequiredClaim(ClaimTypes.Role);

    private string RequiredClaim(string claimType) =>
        (IsAuthenticated ? httpContextAccessor.HttpContext?.User.FindFirstValue(claimType) : null)
        ?? throw new InvalidOperationException($"No signed-in user with a '{claimType}' claim.");
}
