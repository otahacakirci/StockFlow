using StockFlow.Services.Common;

namespace StockFlow.Tests.Services.Common;

public sealed class ListPagingPolicyTests
{
    [Fact]
    public void Normalize_AppliesDefaultsAndMaximumPageSize()
    {
        var missing = ListPagingPolicy.Normalize(null, null);
        var invalid = ListPagingPolicy.Normalize(0, -1);
        var capped = ListPagingPolicy.Normalize(int.MaxValue, int.MaxValue);

        Assert.Equal(new NormalizedPageRequest(1, 20), missing);
        Assert.Equal(new NormalizedPageRequest(1, 20), invalid);
        Assert.Equal(new NormalizedPageRequest(int.MaxValue, 100), capped);
    }

    [Fact]
    public void Resolve_WhenResultIsEmpty_ReturnsFirstPageWithNoTotalPages()
    {
        var page = ListPagingPolicy.Resolve(
            new NormalizedPageRequest(int.MaxValue, 20),
            totalCount: 0);

        Assert.Equal(new ResolvedPage(1, 20, 0, 0), page);
    }

    [Theory]
    [InlineData(1, 20, 21, 1, 2, 0)]
    [InlineData(2, 20, 21, 2, 2, 20)]
    [InlineData(int.MaxValue, 20, 21, 2, 2, 20)]
    public void Resolve_ComputesTotalPagesAndClampsRequestedPage(
        int requestedPage,
        int pageSize,
        int totalCount,
        int expectedPage,
        int expectedTotalPages,
        int expectedOffset)
    {
        var page = ListPagingPolicy.Resolve(
            new NormalizedPageRequest(requestedPage, pageSize),
            totalCount);

        Assert.Equal(expectedPage, page.Page);
        Assert.Equal(pageSize, page.PageSize);
        Assert.Equal(expectedTotalPages, page.TotalPages);
        Assert.Equal(expectedOffset, page.Offset);
    }
}
