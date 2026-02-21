using Core.Helpers;
using FluentAssertions;
using Xunit;

namespace Tests.Helpers;

public class PaginationTests
{
    #region TotalPages

    [Fact]
    public void TotalPages_ExactDivision_ReturnsExact()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 20,
            PageSize = 10
        };

        pagination.TotalPages.Should().Be(2);
    }

    [Fact]
    public void TotalPages_WithRemainder_RoundsUp()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 21,
            PageSize = 10
        };

        pagination.TotalPages.Should().Be(3);
    }

    [Fact]
    public void TotalPages_ZeroTotalCount_ReturnsZero()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 0,
            PageSize = 10
        };

        pagination.TotalPages.Should().Be(0);
    }

    [Fact]
    public void TotalPages_ZeroPageSize_ReturnsZero()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 100,
            PageSize = 0
        };

        pagination.TotalPages.Should().Be(0);
    }

    [Fact]
    public void TotalPages_SingleItem_ReturnsOne()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 1,
            PageSize = 10
        };

        pagination.TotalPages.Should().Be(1);
    }

    [Fact]
    public void TotalPages_PageSizeEqualsTotal_ReturnsOne()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 10,
            PageSize = 10
        };

        pagination.TotalPages.Should().Be(1);
    }

    [Fact]
    public void TotalPages_PageSizeLargerThanTotal_ReturnsOne()
    {
        var pagination = new Pagination<string>
        {
            TotalCount = 3,
            PageSize = 50
        };

        pagination.TotalPages.Should().Be(1);
    }

    #endregion

    #region Defaults

    [Fact]
    public void NewPagination_HasEmptyData()
    {
        var pagination = new Pagination<int>();

        pagination.Data.Should().NotBeNull().And.BeEmpty();
        pagination.PageIndex.Should().Be(0);
        pagination.PageSize.Should().Be(0);
        pagination.TotalCount.Should().Be(0);
    }

    #endregion
}
