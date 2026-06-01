using FluentAssertions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.UnitTests.Application;

public class PageRequestTests
{
    [Theory]
    [InlineData(null, null, 1, 20)]    // defaults
    [InlineData(0, 0, 1, 1)]           // page floors to 1, limit floors to 1
    [InlineData(-5, -5, 1, 1)]         // negatives floor
    [InlineData(3, 50, 3, 50)]         // passthrough
    [InlineData(2, 9999, 2, 100)]      // limit hard-capped at 100
    public void Normalizes_page_and_limit(int? page, int? limit, int expectedPage, int expectedLimit)
    {
        var request = new PageRequest(page, limit);
        request.Page.Should().Be(expectedPage);
        request.Limit.Should().Be(expectedLimit);
    }

    [Fact]
    public void Skip_is_zero_based_offset()
    {
        new PageRequest(1, 20).Skip.Should().Be(0);
        new PageRequest(3, 20).Skip.Should().Be(40);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(10, 20, 1)]
    [InlineData(41, 20, 3)]
    public void TotalPages_rounds_up(int total, int limit, int expected) =>
        new PagedResult<string>([], total, 1, limit).TotalPages.Should().Be(expected);
}
