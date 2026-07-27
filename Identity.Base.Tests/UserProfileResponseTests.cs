using System.Text.Json;
using Identity.Base.Features.Users;
using Shouldly;

namespace Identity.Base.Tests;

public class UserProfileResponseTests
{
    [Fact]
    public void Serialization_IncludesCreatedAt()
    {
        var createdAt = new DateTimeOffset(2026, 7, 27, 12, 30, 0, TimeSpan.Zero);
        var response = new UserProfileResponse(
            Guid.NewGuid(),
            "person@example.com",
            true,
            "Person",
            new Dictionary<string, string?> { ["fullName"] = "Person" },
            "stamp",
            false,
            createdAt);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(createdAt);
    }
}
