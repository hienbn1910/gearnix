namespace Gearnix.Api.Modules.Identity.Dtos;

/// <summary>Who the signed-in user is; the frontend uses the role to choose pages.</summary>
public sealed record MeResponse(Guid Id, string Email, string FullName, string Role);
