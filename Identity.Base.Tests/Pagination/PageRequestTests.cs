using System.Collections.Generic;
using Identity.Base.Abstractions.Pagination;
using Shouldly;
using Xunit;
using PaginationSortDirection = Identity.Base.Abstractions.Pagination.SortDirection;

namespace Identity.Base.Tests.Pagination;

public class PageRequestTests
{
    [Fact]
    public void Create_Applies_Defaults_WhenValuesMissing()
    {
        var request = PageRequest.Create(null, null, null, null, defaultPageSize: 50, maxPageSize: 100);

        request.Page.ShouldBe(1);
        request.PageSize.ShouldBe(50);
        request.Search.ShouldBeNull();
        request.Sorts.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-5, 0, 1, 25)]
    [InlineData(0, 500, 1, 200)]
    [InlineData(3, 75, 3, 75)]
    public void Create_Normalizes_Page_And_PageSize(int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var request = PageRequest.Create(page, pageSize, null, null);

        request.Page.ShouldBe(expectedPage);
        request.PageSize.ShouldBe(expectedPageSize);
    }

    [Fact]
    public void Create_Trims_Search()
    {
        var request = PageRequest.Create(1, 25, "  hello  ", null);
        request.Search.ShouldBe("hello");
    }

    [Theory]
    [InlineData(new[] { "createdAt:desc" }, "createdat", PaginationSortDirection.Descending)]
    [InlineData(new[] { "-createdAt" }, "createdat", PaginationSortDirection.Descending)]
    [InlineData(new[] { "name", "createdAt:asc" }, "createdat", PaginationSortDirection.Ascending)]
    public void Create_Parses_SortExpressions(IEnumerable<string> sort, string expectedField, PaginationSortDirection expectedDirection)
    {
        var request = PageRequest.Create(1, 25, null, sort);
        request.Sorts.ShouldContain(s => s.Field.ToLowerInvariant() == expectedField && s.Direction == expectedDirection);
    }

    [Fact]
    public void Create_Ignores_InvalidSortExpressions()
    {
        var request = PageRequest.Create(1, 25, null, new[] { string.Empty, "   " });
        request.Sorts.ShouldBeEmpty();
    }

    [Fact]
    public void GetSkip_Computes_CorrectValue()
    {
        var request = PageRequest.Create(3, 20, null, null);
        request.GetSkip().ShouldBe(40);
    }

    [Fact]
    public void WithDefaults_Reapplies_Limits()
    {
        var request = PageRequest.Create(5, 500, null, null, defaultPageSize: 10, maxPageSize: 50);
        var normalized = request.WithDefaults(25, 100);
        normalized.Page.ShouldBe(5);
        normalized.PageSize.ShouldBe(50);
    }
}
