namespace Gearnix.Api.Modules.Identity;

/// <summary>
/// The signed-in user of the current request (Workflow.md §5.2). Modules take the user id from here,
/// never from the request body or query string (Rule.md §5).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The user id. Throws when nobody is signed in; use only on endpoints that require sign-in.</summary>
    Guid Id { get; }

    /// <summary>The user's single role (see Common.Auth.Roles). Throws when nobody is signed in.</summary>
    string Role { get; }
}
