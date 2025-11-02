using Shouldly;
using Identity.Base.Organisations.Data;
using Identity.Base.Organisations.Domain;
using Identity.Base.Organisations.Services;
using Microsoft.EntityFrameworkCore;

namespace Identity.Base.Organisations.Tests.Services;

public class OrganisationScopeResolverTests
{
    [Fact]
    public async Task IsInScopeAsync_ReturnsTrue_WhenMembershipExists()
    {
        await using var context = CreateContext(out var organisationId, out var userId);
        var resolver = new OrganisationScopeResolver(context);

        var result = await resolver.IsInScopeAsync(userId, organisationId);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsInScopeAsync_ReturnsFalse_WhenMembershipMissing()
    {
        await using var context = CreateContext(out var organisationId, out _);
        var resolver = new OrganisationScopeResolver(context);

        var result = await resolver.IsInScopeAsync(Guid.NewGuid(), organisationId);

        result.ShouldBeFalse();
    }

    private static OrganisationDbContext CreateContext(out Guid organisationId, out Guid userId)
    {
        var options = new DbContextOptionsBuilder<OrganisationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new OrganisationDbContext(options);

        organisationId = Guid.NewGuid();
        userId = Guid.NewGuid();

        context.Organisations.Add(new Organisation
        {
            Id = organisationId,
            Slug = "org",
            DisplayName = "Org",
            Metadata = OrganisationMetadata.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        context.OrganisationMemberships.Add(new OrganisationMembership
        {
            OrganisationId = organisationId,
            UserId = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        context.SaveChanges();
        return context;
    }
}
