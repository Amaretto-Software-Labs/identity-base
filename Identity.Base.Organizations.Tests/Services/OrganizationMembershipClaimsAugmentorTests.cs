using System.Security.Claims;
using Identity.Base.Identity;
using Identity.Base.Organizations.Abstractions;
using Identity.Base.Organizations.Claims;
using Identity.Base.Organizations.Data;
using Identity.Base.Organizations.Domain;
using Identity.Base.Organizations.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Identity.Base.Organizations.Tests.Services;

public class OrganizationMembershipClaimsAugmentorTests
{
    [Fact]
    public async Task AugmentAsync_AddsMembershipClaim_WhenMembershipsExist()
    {
        await using var context = CreateContext();
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var organizationId = Guid.NewGuid();

        context.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var augmentor = new OrganizationMembershipClaimsAugmentor(context);

        await augmentor.AugmentAsync(user, principal);

        var claim = principal.FindFirst(OrganizationClaimTypes.OrganizationMemberships);
        claim.ShouldNotBeNull();
        claim!.Value.ShouldBe(organizationId.ToString("D"));
    }

    [Fact]
    public async Task AugmentAsync_DoesNothing_WhenNoMemberships()
    {
        await using var context = CreateContext();
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var augmentor = new OrganizationMembershipClaimsAugmentor(context);

        await augmentor.AugmentAsync(user, principal);

        principal.FindFirst(OrganizationClaimTypes.OrganizationMemberships).ShouldBeNull();
    }

    private static OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase($"org-membership-{Guid.NewGuid()}")
            .Options;
        return new OrganizationDbContext(options);
    }
}
