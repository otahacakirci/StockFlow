namespace StockFlow.Services.Common;

/// <summary>
/// Liste sorgularının ortak sayfa varsayılanlarını, üst sınırını ve sonuç penceresini yönetir.
/// </summary>
internal static class ListPagingPolicy
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    internal static NormalizedPageRequest Normalize(int? page, int? pageSize)
    {
        return new NormalizedPageRequest(
            page is > 0 ? page.Value : 1,
            pageSize is > 0 ? Math.Min(pageSize.Value, MaximumPageSize) : DefaultPageSize);
    }

    internal static ResolvedPage Resolve(NormalizedPageRequest request, int totalCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        var totalPages = totalCount == 0
            ? 0
            : (int)((totalCount + (long)request.PageSize - 1) / request.PageSize);
        var page = totalPages == 0
            ? 1
            : Math.Min(request.Page, totalPages);

        return new ResolvedPage(
            page,
            request.PageSize,
            totalPages,
            (page - 1) * request.PageSize);
    }
}

internal readonly record struct NormalizedPageRequest(int Page, int PageSize);

internal readonly record struct ResolvedPage(
    int Page,
    int PageSize,
    int TotalPages,
    int Offset);
