namespace Gearnix.Api.Common.Paging;

/// <summary>
/// Page parameters of a page-based list endpoint, bound from the query string (?page=2&amp;pageSize=50).
/// Every list endpoint is paginated (Rule.md §3, Workflow.md §9).
/// </summary>
public sealed class PageRequest
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
