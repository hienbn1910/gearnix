namespace Gearnix.Api.Common.Errors;

/// <summary>The kind of an expected business failure; it decides the HTTP status.</summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    NotFound,
    Conflict,
    Forbidden,
}
