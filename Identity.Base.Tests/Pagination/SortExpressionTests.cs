using Identity.Base.Abstractions.Pagination;
using Shouldly;
using Xunit;
using PaginationSortDirection = Identity.Base.Abstractions.Pagination.SortDirection;

namespace Identity.Base.Tests.Pagination;

public class SortExpressionTests
{
    [Theory]
    [InlineData("createdAt", "createdAt", PaginationSortDirection.Ascending)]
    [InlineData("createdAt:desc", "createdAt", PaginationSortDirection.Descending)]
    [InlineData("-createdAt", "createdAt", PaginationSortDirection.Descending)]
    [InlineData("name:asc", "name", PaginationSortDirection.Ascending)]
    public void TryParse_ReturnsExpression_WhenValid(string input, string expectedField, PaginationSortDirection expectedDirection)
    {
        var result = SortExpression.TryParse(input, out var expression);
        result.ShouldBeTrue();
        expression.Field.ShouldBe(expectedField);
        expression.Direction.ShouldBe(expectedDirection);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":asc")]
    public void TryParse_ReturnsFalse_WhenInvalid(string input)
    {
        var result = SortExpression.TryParse(input, out _);
        result.ShouldBeFalse();
    }

    [Fact]
    public void From_Throws_WhenInvalid()
    {
        Should.Throw<ArgumentException>(() => SortExpression.From(":invalid"));
    }
}
