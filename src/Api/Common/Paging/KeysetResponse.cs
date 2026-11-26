namespace Gearnix.Api.Common.Paging;

/// <summary>
/// A keyset list response: { items, nextCursor } (Workflow.md §9). NextCursor is null on the last page.
/// </summary>
public sealed record KeysetResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
