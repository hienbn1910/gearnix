namespace Gearnix.Api.Common.Errors;

/// <summary>
/// An expected business failure (e.g. out of stock, invalid status), returned instead of thrown (Rule.md §7).
/// </summary>
/// <param name="Code">Stable code sent to the client, e.g. "out_of_stock" (Workflow.md §9).</param>
/// <param name="Kind">Decides the HTTP status.</param>
/// <param name="Detail">Optional English message for the ProblemDetails "detail" field.</param>
public sealed record Error(string Code, ErrorKind Kind, string? Detail = null);
