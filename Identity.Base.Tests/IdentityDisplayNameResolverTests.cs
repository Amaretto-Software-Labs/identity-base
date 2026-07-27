using Identity.Base.Identity;
using Identity.Base.Options;
using Shouldly;

namespace Identity.Base.Tests;

public class IdentityDisplayNameResolverTests
{
    [Fact]
    public void Resolve_UsesConfiguredFullName()
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fullName"] = "  Full Name User  "
        };
        var fields = new[]
        {
            new RegistrationProfileFieldOptions { Name = "fullName", DisplayName = "Full name" }
        };

        IdentityDisplayNameResolver.Resolve(metadata, fields).ShouldBe("Full Name User");
    }

    [Fact]
    public void Resolve_PrefersDisplayName_WhenBothAliasesAreConfigured()
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] = "Preferred Name",
            ["fullName"] = "Full Name"
        };
        var fields = new[]
        {
            new RegistrationProfileFieldOptions { Name = "fullName", DisplayName = "Full name" },
            new RegistrationProfileFieldOptions { Name = "displayName", DisplayName = "Display name" }
        };

        IdentityDisplayNameResolver.Resolve(metadata, fields).ShouldBe("Preferred Name");
    }
}
