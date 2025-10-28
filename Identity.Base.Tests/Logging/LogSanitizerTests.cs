using Shouldly;
using Identity.Base.Logging;
using Xunit;

namespace Identity.Base.Tests.Logging;

public class LogSanitizerTests
{
    private readonly LogSanitizer _sanitizer = new();

    [Theory]
    [InlineData("user@example.com", "u***r@example.com")]
    [InlineData("ab@example.com", "**@example.com")]
    [InlineData("invalid", "[redacted]")]
    public void RedactEmail_MasksLocalPart(string input, string expected)
    {
        _sanitizer.RedactEmail(input).ShouldBe(expected);
    }

    [Fact]
    public void RedactEmail_AllowsNull()
    {
        _sanitizer.RedactEmail(null).ShouldBeNull();
    }

    [Theory]
    [InlineData("+1-555-123-4567", "***4567")]
    [InlineData("1234", "***1234")]
    [InlineData("abc", "[redacted]")]
    public void RedactPhoneNumber_ShowsLastDigits(string input, string expected)
    {
        _sanitizer.RedactPhoneNumber(input).ShouldBe(expected);
    }

    [Fact]
    public void RedactToken_AlwaysRedacts()
    {
        _sanitizer.RedactToken("secret").ShouldBe("[redacted]");
    }
}
