namespace Gearnix.Api.Common.Paging;

/// <summary>A page-based list response: { items, page, pageSize, totalCount } (Workflow.md §9).</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
