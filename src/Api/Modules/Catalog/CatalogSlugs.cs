namespace Gearnix.Api.Modules.Catalog;

/// <summary>Picks a free slug when several items share a name (Workflow.md §6.1).</summary>
public static class CatalogSlugs
{
    /// <summary>
    /// <paramref name="baseSlug"/> itself if free, otherwise the first free of <c>-2</c>, <c>-3</c>, …
    /// </summary>
    /// <param name="baseSlug">The slug generated from the name.</param>
    /// <param name="taken">Existing slugs equal to the base slug or starting with <c>baseSlug-</c>.</param>
    public static string PickFree(string baseSlug, IReadOnlyCollection<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(baseSlug))
        {
            return baseSlug;
        }

        // At most taken.Count + 1 candidates are needed, so the loop always ends.
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseSlug}-{suffix}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
