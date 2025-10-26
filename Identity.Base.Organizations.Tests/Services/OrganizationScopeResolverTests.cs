using FluentAssertions;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organizations.Tests.Services;

public class OrganizationScopeResolverTests
{
    [Fact]
    public async Task IsInScopeAsync_ReturnsTrue_WhenMembershipExists()
    {
        await using var context = CreateContext(out var organizationId, out var userId);
        var resolver = new OrganizationScopeResolver(context);

        var result = await resolver.IsInScopeAsync(userId, organizationId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInScopeAsync_ReturnsFalse_WhenMembershipMissing()
    {
        await using var context = CreateContext(out var organizationId, out _);
        var resolver = new OrganizationScopeResolver(context);

        var result = await resolver.IsInScopeAsync(Guid.NewGuid(), organizationId);

        result.Should().BeFalse();
    }

    private static OrganizationDbContext CreateContext(out Guid organizationId, out Guid userId)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new OrganizationDbContext(options);

        organizationId = Guid.NewGuid();
        userId = Guid.NewGuid();

        context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Slug = "org",
            DisplayName = "Org",
            Metadata = OrganizationMetadata.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        context.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        context.SaveChanges();
        return context;
    }
}
