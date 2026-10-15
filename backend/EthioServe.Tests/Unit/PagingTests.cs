using EthioServe.Application.Common;

namespace EthioServe.Tests.Unit;

public sealed class PagingTests
{
    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-5, 10, 1, 10)]
    [InlineData(3, 1000, 3, 50)]
    [InlineData(int.MaxValue, 50, 10_000, 50)]
    public void Paging_values_are_clamped(int page, int pageSize, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), Paging.Normalize(page, pageSize));
    }

    [Theory]
    [InlineData(0, 12, 0)]
    [InlineData(1, 12, 1)]
    [InlineData(12, 12, 1)]
    [InlineData(13, 12, 2)]
    public void Total_pages_round_up(int totalCount, int pageSize, int expectedPages)
    {
        var result = new PagedResult<int>([], 1, pageSize, totalCount);

        Assert.Equal(expectedPages, result.TotalPages);
    }
}