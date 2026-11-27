namespace Gearnix.Api.Modules.Catalog.Dtos;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    Guid? ParentId,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
