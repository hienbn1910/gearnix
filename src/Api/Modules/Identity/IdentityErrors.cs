using Gearnix.Api.Common.Errors;

namespace Gearnix.Api.Modules.Identity;

/// <summary>Business errors of the Identity module (Workflow.md §9).</summary>
public static class IdentityErrors
{
    public static readonly Error EmailTaken = new("email_taken", ErrorKind.Conflict);

    /// <summary>Same error for unknown email, wrong password and locked account, so nothing is revealed.</summary>
    public static readonly Error InvalidCredentials =
        new("invalid_credentials", ErrorKind.Unauthorized, "Email or password is incorrect.");

    public static readonly Error UserNotFound = new(ErrorCodes.NotFound, ErrorKind.NotFound);
}
