using Gearnix.Api.Common.Errors;

namespace Gearnix.Api.Modules.Catalog;

/// <summary>Business errors of the Catalog module (Workflow.md §9).</summary>
public static class CatalogErrors
{
    public static readonly Error NotFound = new(ErrorCodes.NotFound, ErrorKind.NotFound);

    public static readonly Error CategoryNotFound =
        new("category_not_found", ErrorKind.Validation, "The category does not exist.");

    public static readonly Error InvalidCategoryParent = new("invalid_category_parent", ErrorKind.Validation,
        "Categories are at most two levels deep: the parent must be a different top-level category, "
        + "and a category with children cannot get a parent.");

    public static readonly Error SlugConflict = new("slug_conflict", ErrorKind.Conflict,
        "Another item with the same name was created at the same moment. Try again.");

    public static readonly Error SkuCodeTaken =
        new("sku_code_taken", ErrorKind.Conflict, "Another SKU already uses this SKU code.");
}
