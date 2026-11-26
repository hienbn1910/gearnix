using System.Diagnostics.CodeAnalysis;

namespace Gearnix.Api.Common.Errors;

/// <summary>The outcome of an operation with no value: success, or an expected <see cref="Errors.Error"/>.</summary>
public sealed class Result
{
    private Result(Error? error) => Error = error;

    public static Result Success { get; } = new(null);

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static implicit operator Result(Error error) => new(error);
}
