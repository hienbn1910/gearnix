using System.Text.Json;

namespace Gearnix.Api.Modules.Catalog.Validators;

/// <summary>Limits for the flat JSON objects <c>Specs</c> and <c>Attributes</c>, so bad input cannot bloat rows.</summary>
public static class CatalogJsonRules
{
    public const int MaxSpecs = 30;
    public const int MaxAttributes = 10;
    public const int KeyMaxLength = 50;
    public const int StringValueMaxLength = 200;

    public const string SpecsMessage =
        "Specs must be a flat object of at most 30 keys (1–50 characters each) "
        + "whose values are strings of at most 200 characters, numbers or booleans.";

    public const string AttributesMessage =
        "Attributes must be a flat object of at most 10 keys (1–50 characters each) "
        + "whose values are strings of at most 200 characters.";

    public static bool AreValidSpecs(Dictionary<string, JsonElement>? specs) =>
        specs is null
        || (specs.Count <= MaxSpecs && specs.All(spec => IsValidKey(spec.Key) && IsValidSpecValue(spec.Value)));

    public static bool AreValidAttributes(Dictionary<string, string>? attributes) =>
        attributes is null
        || (attributes.Count <= MaxAttributes
            && attributes.All(attribute => IsValidKey(attribute.Key) && IsValidString(attribute.Value)));

    private static bool IsValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= KeyMaxLength;

    // Objects, arrays and null are rejected: specs are shown as a simple two-column table.
    private static bool IsValidSpecValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => IsValidString(value.GetString()),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => true,
        _ => false,
    };

    private static bool IsValidString(string? value) => value is not null && value.Length <= StringValueMaxLength;
}
