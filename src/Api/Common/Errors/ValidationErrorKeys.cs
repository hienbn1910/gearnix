using System.Text.Json;

namespace Gearnix.Api.Common.Errors;

/// <summary>
/// Field names in the "errors" map of a validation_failed response are camelCase, the way the client wrote them
/// in the JSON body or the query string (Workflow.md §9).
/// </summary>
/// <remarks>
/// Done once, on the finished response, because errors arrive keyed by C# property names from two places:
/// FluentValidation ("PageSize") and model binding of query values ("The value 'abc' is not valid…").
/// It runs for every validation problem built by <c>ProblemDetailsFactory</c>; one built by hand with
/// <c>new ValidationProblemDetails(...)</c> would skip it, so always go through the factory.
/// </remarks>
public static class ValidationErrorKeys
{
    /// <summary>Rewrites every key of the map in place to camelCase; keys that become equal keep all their messages.</summary>
    public static void RewriteKeysToCamelCase(IDictionary<string, string[]> errors)
    {
        var original = errors.ToList();
        errors.Clear();

        foreach (var (key, messages) in original)
        {
            var camelKey = ToCamelCasePath(key);
            errors[camelKey] = errors.TryGetValue(camelKey, out var existing) ? [.. existing, .. messages] : messages;
        }
    }

    /// <summary>"Items[0].UnitPrice" → "items[0].unitPrice".</summary>
    private static string ToCamelCasePath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
