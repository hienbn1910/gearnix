using System.Text.Json;

namespace Gearnix.Api.Modules.Catalog.Dtos;

/// <summary>Create or fully replace a product. The slug and min price are set by the server and never sent.</summary>
/// <param name="Specs">Flat object of strings, numbers and booleans, e.g. <c>{"cpu": "N1", "ramGb": 16}</c>.</param>
public sealed record ProductRequest(
    Guid CategoryId,
    string Name,
    string Brand,
    string? Description,
    Dictionary<string, JsonElement>? Specs,
    bool IsActive = true);
